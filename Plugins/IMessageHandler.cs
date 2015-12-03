using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using STOMP.Server.Clients;
using STOMP.Frames;
using STOMP.Server;

namespace STOMP.Server.Plugins
{
    interface IMessageHandler
    {
        void ReceiveMessage(ClientConnection Sender, StompMessageFrame Frame);
    }
}
