using System; 
using System.IO; 
using System.Net; 
using System.Net.Sockets; 
using System.Text; 
using System.Text.Json; 
using System.Threading; 
using System.Threading.Tasks; 
using Macad.Core; 
using Macad.Core.Shapes; 
using Macad.Core.Topology; 
using Macad.Interaction; 
using Macad.Occt; 
 
namespace Macad.Plugin.GeometryListener
{
    public static class GeometryListener 
    { 
        private static CancellationTokenSource _cts; 
    
        public static void Start() 
        { 
            Stop(); 
            _cts = new CancellationTokenSource(); 
            _ = ListenAsync(_cts.Token); 
            Messages.Info("Geometry Listener 正在监听 127.0.0.1:12345..."); 
        } 
    
        public static void Stop() 
        { 
            if (_cts != null) 
            { 
                _cts.Cancel(); 
                _cts.Dispose(); 
                _cts = null; 
                Messages.Info("Geometry Listener 已停止。"); 
            } 
        } 
    
        private static async Task ListenAsync(CancellationToken token) 
        { 
            TcpListener listener = null; 
            try 
            { 
                listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 12345); 
                listener.Start(); 
    
                while (!token.IsCancellationRequested) 
                { 
                    if (!listener.Pending()) 
                    { 
                        await Task.Delay(100, token); 
                        continue; 
                    } 
    
                    using var client = await listener.AcceptTcpClientAsync(); 
                    using var stream = client.GetStream(); 
                    using var reader = new StreamReader(stream, Encoding.UTF8); 
                    
                    var json = await reader.ReadToEndAsync(); 
                    if (string.IsNullOrWhiteSpace(json)) continue; 
    
                    ProcessPackage(json); 
                } 
            } 
            catch (Exception ex) 
            { 
                if (!token.IsCancellationRequested) 
                    Messages.Error($"监听器错误: {ex.Message}"); 
            } 
            finally 
            { 
                listener?.Stop(); 
            } 
        } 
    
        private static void ProcessPackage(string json) 
        { 
            try 
            { 
                // 立即反序列化提取数据，避免跨线程使用 disposed 的 JsonDocument 和 JsonElement
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var package = JsonSerializer.Deserialize<GeometryListPackageDto>(json, options);

                if (package?.Data == null || package.Data.Count == 0)
                {
                    // 尝试看是不是直接发的一个数组，或者是没有被包在 Data 里的格式
                    var document = JsonDocument.Parse(json);
                    var root = document.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        // 兼容直接是数组的情况
                    }
                    else
                    {
                        Messages.Warning("数据格式错误：未能解析到包含数据的 Data 字段。");
                        return;
                    }
                }

                Messages.Info($"接收到数据包，尝试在主线程渲染...");

                // 在 Macad 的主线程中执行文档修改 
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                { 
                    try
                    {
                        // 重新解析一份 JsonDocument，因为它的生命周期必须在这个线程的闭包里
                        using var document = JsonDocument.Parse(json);
                        var root = document.RootElement;

                        JsonElement geometries;
                        if (root.TryGetProperty("Data", out var dataProp1) && dataProp1.ValueKind == JsonValueKind.Array)
                        {
                            geometries = dataProp1;
                        }
                        else if (root.TryGetProperty("data", out var dataProp2) && dataProp2.ValueKind == JsonValueKind.Array)
                        {
                            geometries = dataProp2;
                        }
                        else if (root.TryGetProperty("Geometries", out var geoProp) && geoProp.ValueKind == JsonValueKind.Array)
                        {
                            geometries = geoProp;
                        }
                        else if (root.ValueKind == JsonValueKind.Array)
                        {
                            geometries = root;
                        }
                        else
                        {
                            Messages.Warning("在主线程中未找到有效的几何数组数据。");
                            return;
                        }

                        Messages.Info($"准备处理 {geometries.GetArrayLength()} 个几何体");

                        foreach (var item in geometries.EnumerateArray()) 
                        { 
                            if (!item.TryGetProperty("type", out var typeProp) || !item.TryGetProperty("payload", out var payloadProp)) 
                            {
                                Messages.Warning("跳过：缺少 type 或 payload 字段。");
                                continue; 
                            }
        
                            var type = typeProp.GetString(); 
                            Messages.Info($"处理类型: {type}");

                            if (type == "occt_brep_base64") 
                            { 
                                var base64 = payloadProp.GetString(); 
                                if (string.IsNullOrEmpty(base64)) 
                                {
                                    Messages.Warning("跳过：base64 payload 为空。");
                                    continue; 
                                }
        
                                Messages.Info("开始解析 base64 数据并保存到临时文件...");
                                var bytes = Convert.FromBase64String(base64); 
                                var tempFile = Path.GetTempFileName(); 
                                File.WriteAllBytes(tempFile, bytes); 
        
                                Messages.Info("开始调用 BRepTools.Read 读取 OCCT BRep...");
                                // 读取 OCCT BRep 并创建 Macad Body 
                                var shape = new TopoDS_Shape(); 
                                var builder = new BRep_Builder(); 
                                if (BRepTools.Read(shape, tempFile, builder, new Message_ProgressRange())) 
                                { 
                                    Messages.Info("BRepTools.Read 成功，开始创建 Solid 和 Body...");
                                    // 将 OCCT Shape 包装成 Macad 可识别的 Solid 和 Body 
                                    var solid = Solid.Create(shape); 
                                    
                                    // 必须强制执行构建，否则没有任何数据用于渲染
                                    solid.Make(Shape.MakeFlags.None);

                                    var body = Body.Create(solid); 
                                    InteractiveContext.Current.Document.Add(body); 
                                    
                                    Messages.Info("Body 已添加到文档，准备提升状态...");
                                    // 确保模型添加到文档后，立刻提升状态以便重新构建显示数据，并触发重绘
                                    body.RaiseVisualChanged();
                                    InteractiveContext.Current.WorkspaceController.Selection.SelectEntity(body);
                                    
                                    var bbox = solid.GetBRep()?.BoundingBox();
                                    if (bbox != null)
                                    {
                                        double xmin = 0, ymin = 0, zmin = 0, xmax = 0, ymax = 0, zmax = 0;
                                        bbox.Get(ref xmin, ref ymin, ref zmin, ref xmax, ref ymax, ref zmax);
                                        Messages.Info($"成功添加几何体。包围盒：[{xmin:F2}, {ymin:F2}, {zmin:F2}] -> [{xmax:F2}, {ymax:F2}, {zmax:F2}]");
                                    }
                                    else
                                    {
                                        Messages.Info("成功添加了一个几何体到文档，但无法获取包围盒。");
                                    }
                                } 
                                else
                                {
                                    Messages.Error("BRepTools.Read 失败：无法解析 OCCT BRep 数据。");
                                }
                                
                                try { File.Delete(tempFile); } catch { } 
                            } 
                            else
                            {
                                Messages.Warning($"跳过未知的几何体类型: {type}");
                            }
                            // Mesh 和 Plane 目前以 JSON 结构传入，Macad 如果需要渲染它们可以继续在这里扩展 
                        } 
                        
                        // 刷新视图 
                        InteractiveContext.Current.WorkspaceController.Invalidate(); 
                        Messages.Info("视图已刷新。");
                    }
                    catch (Exception ex)
                    {
                        Messages.Error($"主线程渲染失败: {ex.Message}"); 
                    }
                }); 
            } 
            catch (Exception ex) 
            { 
                Messages.Error($"解析包失败: {ex.Message}"); 
            } 
        } 

        // 用于验证反序列化
        private class GeometryListPackageDto
        {
            public System.Collections.Generic.List<object> Data { get; set; }
            public string Description { get; set; }
        }
    }
}
