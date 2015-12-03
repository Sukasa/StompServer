using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using STOMP.Frames;
using System.Threading.Tasks;

namespace STOMP.Server.Clients
{
    public class ClientSubscription
    {
        public Regex Filter { get; private set; }
        public string Id { get; private set; }
        public AcknowledgementType AckType { get; private set; }

        public enum AcknowledgementType
        {
            None,
            Client,
            ClientIndividual
        }

        public ClientSubscription(StompSubscribeFrame SubscriptionFrame)
        {
            Filter = new Regex(SubscriptionFrame.Destination);
            Id = SubscriptionFrame.Id;

            switch (SubscriptionFrame.AckSetting.ToLower())
            {
                case "client-individual":
                    AckType = AcknowledgementType.ClientIndividual;
                    break;
                case "client":
                    AckType = AcknowledgementType.Client;
                    break;
                default:
                    AckType = AcknowledgementType.None;
                    break;
            }
        }
    }
}
