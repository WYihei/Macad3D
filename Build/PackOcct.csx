#load "_Common.csx"

using System.Text.RegularExpressions;

// Usage: packocct [/push <serverUrl>] [/apikey <key>]
//
// Creates a NuGet package (Macad.OcctWrapper) from the staged files in bin\OcctPackage\.
// The package declares a dependency on Macad.ThirdParty.Occt (native OCCT DLLs), whose
// version is read from Directory.Packages.props.
//
// Run 'buildocct' before running this script.
//
// Options:
//   /push <serverUrl>   Push the resulting .nupkg to a NuGet server.
//   /apikey <key>       API key for the push (optional if the server allows anonymous push).
//
// Notes:
//   * Consumers of the package also need access to Macad.ThirdParty.Occt.
//     Either mirror it to your self-hosted server or add nuget.macad3d.net as a source.
//   * The generated .csproj used for packing is placed in .intermediate\OcctPack\ and
//     cleaned up automatically.

//--------------------------------------------------------------------------------------------------

string optionPushUrl = null;
string optionApiKey  = null;

for (int i = 0; i < Args.Count; i++)
{
    var arg = Args[i];
    if (arg.Equals("/push", StringComparison.OrdinalIgnoreCase) && i + 1 < Args.Count)
        optionPushUrl = Args[++i];
    else if (arg.Equals("/apikey", StringComparison.OrdinalIgnoreCase) && i + 1 < Args.Count)
        optionApiKey = Args[++i];
    else if (arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
    {
        Printer.Line("Usage: packocct [/push <serverUrl>] [/apikey <key>]");
        return 0;
    }
}

//--------------------------------------------------------------------------------------------------

var rootDir = Common.GetRootFolder();

// Read Macad version from Macad.VersionInfo.props
var packageVersion = _ReadMacadVersion(rootDir);
if (packageVersion == null)
{
    Printer.Error("Failed to read version from Build\\MSBuild\\Macad.VersionInfo.props.");
    return -1;
}

// Read Macad.ThirdParty.Occt version from Directory.Packages.props
var occtVersion = _ReadPackageVersion(rootDir, "Macad.ThirdParty.Occt");
if (occtVersion == null)
{
    Printer.Error("Failed to read Macad.ThirdParty.Occt version from Directory.Packages.props.");
    return -1;
}

Printer.Line($"Package version  : {packageVersion}");
Printer.Line($"OCCT dependency  : Macad.ThirdParty.Occt {occtVersion}");

//--------------------------------------------------------------------------------------------------

// Check staged files
var stagingDir = Path.Combine(rootDir, @"bin\OcctPackage");
var requiredFiles = new[] { "Macad.Common.dll", "Macad.Occt.dll", "Macad.Managed.dll" };
foreach (var f in requiredFiles)
{
    if (!File.Exists(Path.Combine(stagingDir, f)))
    {
        Printer.Error($"Staged file not found: {f}");
        Printer.Error($"Run 'buildocct' first. Expected location: {stagingDir}");
        return -1;
    }
}

//--------------------------------------------------------------------------------------------------

// Generate temp SDK project in .intermediate\OcctPack\ so nuget.config is inherited from root
var tempDir = Path.Combine(rootDir, @".intermediate\OcctPack");
Directory.CreateDirectory(tempDir);
var tempCsproj = Path.Combine(tempDir, "Macad.OcctWrapper.csproj");
_WritePackingProject(tempCsproj, packageVersion, occtVersion, stagingDir);

//--------------------------------------------------------------------------------------------------

var outputDir = Path.Combine(rootDir, @"bin\NuGetPackages");
Directory.CreateDirectory(outputDir);

Printer.Line($"\nRestoring packages ...");
if (Common.Run("dotnet", $"restore \"{tempCsproj}\"") != 0)
{
    Printer.Error("Restore failed.");
    _Cleanup(tempDir);
    return -1;
}

Printer.Line($"\nPacking Macad.OcctWrapper {packageVersion} ...");
if (Common.Run("dotnet", $"pack \"{tempCsproj}\" --no-build -o \"{outputDir}\"") != 0)
{
    Printer.Error("Pack failed.");
    _Cleanup(tempDir);
    return -1;
}

_Cleanup(tempDir);

// Find the created package
var packageFile = Directory
    .EnumerateFiles(outputDir, $"Macad.OcctWrapper.{packageVersion}.nupkg")
    .FirstOrDefault()
    ?? Directory
    .EnumerateFiles(outputDir, "Macad.OcctWrapper.*.nupkg")
    .OrderByDescending(f => File.GetLastWriteTime(f))
    .FirstOrDefault();

if (packageFile == null)
{
    Printer.Error("Package file not found after packing.");
    return -1;
}

Printer.Success($"Package created: {packageFile}");

//--------------------------------------------------------------------------------------------------

if (optionPushUrl != null)
{
    Printer.Line($"\nPushing to {optionPushUrl} ...");
    var pushArgs = $"nuget push \"{packageFile}\" --source \"{optionPushUrl}\"";
    if (optionApiKey != null)
        pushArgs += $" --api-key \"{optionApiKey}\"";

    if (Common.Run("dotnet", pushArgs) != 0)
    {
        Printer.Error("Push failed.");
        return -1;
    }
    Printer.Success("Package pushed successfully.");
}
else
{
    Printer.Line($"\nTo push later, run:");
    Printer.Line($"  packocct /push <serverUrl> [/apikey <key>]");
    Printer.Line($"Or manually:");
    Printer.Line($"  dotnet nuget push \"{packageFile}\" --source <serverUrl>");
}

return 0;

/***************************************************************/

static string _ReadMacadVersion(string rootDir)
{
    try
    {
        var content = File.ReadAllText(Path.Combine(rootDir, @"Build\MSBuild\Macad.VersionInfo.props"));
        var match = Regex.Match(content, @"<Version>(\d+\.\d+\.\d+)");
        if (!match.Success)
            return null;
        return match.Groups[1].Value;
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
        return null;
    }
}

/***************************************************************/

static string _ReadPackageVersion(string rootDir, string packageId)
{
    try
    {
        var propsFile = Path.Combine(rootDir, "Directory.Packages.props");
        if (!File.Exists(propsFile))
        {
            Console.WriteLine($"File not found: {propsFile}");
            return null;
        }
        var content = File.ReadAllText(propsFile);
        var escaped = Regex.Escape(packageId);
        // Match both attribute orderings: Include first, or Version first
        var match = Regex.Match(content, $@"Include=""{escaped}""\s+Version=""([^""]+)""", RegexOptions.IgnoreCase)
                 ?? Regex.Match(content, $@"Version=""([^""]+)""\s+Include=""{escaped}""", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            Console.WriteLine($"Package '{packageId}' not found in Directory.Packages.props");
            return null;
        }
        return match.Groups[1].Value;
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
        return null;
    }
}

/***************************************************************/

static void _WritePackingProject(string path, string packageVersion, string occtVersion, string stagingDir)
{
    string X(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"    <None Include=\"{X(Path.Combine(stagingDir, "Macad.Common.dll"))}\" Pack=\"true\" PackagePath=\"lib\\\\net10.0-windows7.0\\\\\" />");

    var xmlDoc = Path.Combine(stagingDir, "Macad.Common.xml");
    if (File.Exists(xmlDoc))
        sb.AppendLine($"    <None Include=\"{X(xmlDoc)}\" Pack=\"true\" PackagePath=\"lib\\\\net10.0-windows7.0\\\\\" />");

    sb.AppendLine($"    <None Include=\"{X(Path.Combine(stagingDir, "Macad.Occt.dll"))}\" Pack=\"true\" PackagePath=\"lib\\\\net10.0-windows7.0\\\\\" />");
    sb.AppendLine($"    <None Include=\"{X(Path.Combine(stagingDir, "Macad.Managed.dll"))}\" Pack=\"true\" PackagePath=\"lib\\\\net10.0-windows7.0\\\\\" />");

    var content = $"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows7.0</TargetFramework>
    <UseWPF>false</UseWPF>
    <UseWindowsForms>false</UseWindowsForms>
    <PackageId>Macad.OcctWrapper</PackageId>
    <Version>{X(packageVersion)}</Version>
    <Description>C# / C++/CLI wrapper for OpenCASCADE Technology (OCCT), based on Macad|3D. Provides Macad.Common, Macad.Occt and Macad.Managed assemblies.</Description>
    <Authors>Macad3D Contributors</Authors>
    <PackageTags>occt opencascade cad wrapper cppcli</PackageTags>
    <PackageProjectUrl>https://github.com/Macad3D/Macad3D</PackageProjectUrl>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <NoBuild>true</NoBuild>
    <NoDefaultExcludes>true</NoDefaultExcludes>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <!-- Managed assemblies -->
  <ItemGroup>
{sb}  </ItemGroup>

  <!-- Declares Macad.ThirdParty.Occt (native OCCT DLLs) as a runtime dependency.
       Consumers need access to this package either from nuget.macad3d.net
       or a mirror on their own NuGet server. -->
  <ItemGroup>
    <PackageReference Include="Macad.ThirdParty.Occt" Version="{X(occtVersion)}" />
  </ItemGroup>

</Project>
""";

    File.WriteAllText(path, content);
}

/***************************************************************/

static void _Cleanup(string tempDir)
{
    try { Directory.Delete(tempDir, recursive: true); }
    catch { /* best effort */ }
}
