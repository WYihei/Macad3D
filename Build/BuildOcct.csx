#load "_Common.csx"
#load "_BuildTools.csx"

// Usage: buildocct [/clean]
//
// Builds Macad.Common, Macad.Occt and Macad.Managed projects (Release|x64)
// in dependency order, then stages the output assemblies to bin\OcctPackage\.
// Run 'restore' first if you haven't done so already.

if (Args.Any(s => s.Equals("/?", StringComparison.OrdinalIgnoreCase)))
{
    Printer.Line("Usage: buildocct [/clean]");
    return 0;
}

bool optionClean = Args.Any(s => s.Equals("/clean", StringComparison.OrdinalIgnoreCase));

var vs = new VisualStudio();
if (!vs.IsReady)
    return -1;

var rootDir = Common.GetRootFolder();
const string Config   = "Release";
const string Platform = "x64";

// Dependency order matters: Common → Occt → Managed
var projects = new (string Name, string File)[]
{
    ("Macad.Common",  Path.Combine(rootDir, @"Source\Macad.Common\Macad.Common.csproj")),
    ("Macad.Occt",    Path.Combine(rootDir, @"Source\Macad.Occt\Macad.Occt.vcxproj")),
    ("Macad.Managed", Path.Combine(rootDir, @"Source\Macad.Managed\Macad.Managed.vcxproj")),
};

//--------------------------------------------------------------------------------------------------

if (optionClean)
{
    Printer.Line("Cleaning...");
    foreach (var (name, file) in projects)
    {
        Printer.Line($"  {name}");
        vs.Clean(file, "", Config, Platform);
    }
    Printer.Line("");
}

//--------------------------------------------------------------------------------------------------

foreach (var (name, file) in projects)
{
    Printer.Line($"Building {name}...");
    if (!vs.Build(file, "", Config, Platform, "-restore"))
    {
        Printer.Error($"Build failed: {name}");
        return -1;
    }
}

//--------------------------------------------------------------------------------------------------

var stagingDir = Path.Combine(rootDir, @"bin\OcctPackage");
Directory.CreateDirectory(stagingDir);

var sourceDir = Path.Combine(rootDir, $@"bin\{Config}");

var filesToStage = new[]
{
    "Macad.Common.dll",
    "Macad.Common.xml",   // XML doc, may not exist in all configs
    "Macad.Occt.dll",
    "Macad.Managed.dll",
};

Printer.Line($"\nStaging to {stagingDir} ...");
foreach (var file in filesToStage)
{
    var src = Path.Combine(sourceDir, file);
    if (File.Exists(src))
    {
        File.Copy(src, Path.Combine(stagingDir, file), overwrite: true);
        Printer.Success($"  + {file}");
    }
    else
    {
        Printer.Warning($"  - {file} (not found, skipped)");
    }
}

Printer.Success("\nOCCT wrapper build complete.");
Printer.Line($"Staged output: {stagingDir}");
Printer.Line("Next step: run 'packocct' to create the NuGet package.");
return 0;
