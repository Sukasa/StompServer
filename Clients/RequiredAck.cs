using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using STOMP.Frames;

namespace STOMP.Server.Clients
{
    public class RequiredAck
    {
        public string SubscriptionId; // What subscription this ack is for
        public string Ack;            // The Ack-Id for this packet
        public StompFrame Frame;      // The frame that was to be sent
        public bool HasTransactedAck; // If true, the ACK/NACK for this frame is queued as part of a transaction
    }
}
