using STOMP;
using STOMP.Frames;
using STOMP.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STOMP.Server.Plugins
{
    public abstract class PluginBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="GrantedPermissions">
        ///     The permissions the plugin has.  Used to return the permissions needed, if the plugin cannot load
        /// </param>
        ///     TRUE if the plugin can be loaded with the given permissions
        /// <returns></returns>
        public abstract bool Load(ref PluginInterface.PluginPermissions[] GrantedPermissions);

        // TODO fill in delegate definitions better
        public delegate void QueryConnectionCancelHook();
        public delegate void ConnectionSuccessHook();
        public delegate void ConnectionFailureHook();

        public delegate void MessageNotificationHook();
        public delegate StompFrame MessageModificationHook();

        
    }
}
