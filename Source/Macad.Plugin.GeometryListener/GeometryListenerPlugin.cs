using System;
using Macad.Common;
using Macad.Core;
using Macad.Core.Plugin;

namespace Macad.Plugin.GeometryListener
{
    public class GeometryListenerPlugin : IPlugin
    {
        public string Name => "Geometry Listener";
        public string Description => "Listens for external geometry data and displays it";
        public Version Version => new Version(1, 0, 0);

        public void Initialize(IPluginContext context)
        {
            Messages.Info($"Plugin '{Name}' v{Version} has been loaded successfully!");
            
            // Start listening
            GeometryListener.Start();
        }

        public void Shutdown()
        {
            // Stop listening
            GeometryListener.Stop();
        }
    }
}
