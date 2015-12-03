using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STOMP.Server.Plugins
{
    interface IPlugin
    {
        bool Load(STOMP.Server.Plugins.PluginInterface.PluginPermissions[] GrantedPermissions);
    }
}
