using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace STOMP.Server.Plugins
{
    public class PluginConfig
    {
        public string PluginFriendlyName;
        public string PluginQualifiedName;

        public bool Loaded;

        public PluginInterface.PluginPermissions[] ActivePermissions;

        public IEnumerable<Regex> MessageSubscriptions;

        internal PluginConfig()
        {

        }
    }
}
