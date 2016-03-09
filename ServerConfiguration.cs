using System.Collections.Generic;
using STOMP.Server.Plugins;
using System.Reflection;

namespace STOMP.Server
{
    class ServerConfiguration
    {
        public IEnumerable<PluginSettings> PluginConfigurations;
        public PluginSettings DefaultConfig = PluginSettings.Default();
        public IDictionary<Assembly, PluginSettings> ConfigByCaller;

        public void CreatePluginConfiguration(Assembly Plugin) {

        }

        public ServerConfiguration()
        {
            ConfigByCaller = new Dictionary<Assembly, PluginSettings>();
            PluginConfigurations = ConfigByCaller.Values;
        }
    }
}