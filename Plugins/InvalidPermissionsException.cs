using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using STOMP.Server;
using System.Threading.Tasks;

namespace STOMP.Server.Plugins
{
    class InvalidPermissionsException : Exception {

        public PluginInterface.PluginPermissions MissingPermission { get; private set; }
        
        public InvalidPermissionsException(string Reason, PluginInterface.PluginPermissions Permission) : base(Reason)
        {  
            MissingPermission = Permission;
        }

        public InvalidPermissionsException(PluginInterface.PluginPermissions Permission)
            : base(string.Format("Missing required permission {0}", Permission))
        {
            MissingPermission = Permission;
        }
    }
}
