using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Reflection;
using System.Threading.Tasks;

namespace STOMP.Server.Plugins
{
    public class PluginSettings
    {
        public string FriendlyName;
        public string QualifiedName;
        [JsonIgnore()] 
        public string FileName;
        [JsonIgnore()]
        public AppDomain Domain;
        public bool Loaded;
        public object PluginCustomSettings;

        public PluginInterface.PluginPermissions[] ActivePermissions;
        public IEnumerable<Regex> MessageSubscriptions;

        public PluginSettings(Assembly PluginAssembly, PluginSettings DefaultConfig)
        {
            AssemblyName Name = new AssemblyName(PluginAssembly.FullName);

            FriendlyName = Name.Name;
            QualifiedName = Name.FullName;

            ActivePermissions = new PluginInterface.PluginPermissions[DefaultConfig.ActivePermissions.Length];
            DefaultConfig.ActivePermissions.CopyTo(ActivePermissions, 0);

            Loaded = false;
            MessageSubscriptions = new List<Regex>();

            FileName = PluginAssembly.Location;
        }

        public PluginSettings()
        {
            
        }

        internal static PluginSettings Default()
        {
            PluginSettings PS = new PluginSettings();

            PS.ActivePermissions = new PluginInterface.PluginPermissions[] { PluginInterface.PluginPermissions.ReceiveMessage, PluginInterface.PluginPermissions.SendMessage };
            
            return PS;
        }
    }
}
