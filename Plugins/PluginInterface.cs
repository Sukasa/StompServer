using STOMP.Frames;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using STOMP.Server.Clients;
using System.Threading.Tasks;
using System.Reflection;

namespace STOMP.Server.Plugins
{
    public class PluginInterface
    {
        private STOMPServer _Server;

        internal PluginInterface(STOMPServer Server)
        {
            _Server = Server;
            PluginConfig = new Dictionary<string, PluginConfig>();

            // TODO Load Plugins here
        }
        
        public bool SendRawFrame(StompFrame Frame)
        {

            return false;
        }

        public bool StopServer()
        {
            return false;
        }

        #region Plugin Management

        private Dictionary<string, PluginConfig> PluginConfig;

        public void SetPermission(PluginConfig Plugin, PluginPermissions Permission, bool PermissionSetting)
        {

        }

        internal PluginPermissions[] RegisterForEvents(Type PluginClass)
        {
            // Check permissions list for this plugin


            // Register for permissions that are marked YES


            // Add any missing permissions strings to the list, set to NO



            return null;
        }

        public void LoadPlugin(PluginConfig Plugin)
        {

        }

        public void UnloadPlugin(PluginConfig Plugin)
        {

        }

        public IEnumerable<PluginConfig> GetPlugins()
        {
            return null;
        }

        public bool HasPermission(Assembly Plugin, PluginPermissions Permission)
        {
            return PluginConfig.ContainsKey(Plugin.GetName().Name) && PluginConfig[Plugin.GetName().Name].ActivePermissions.Contains(Permission);
        }

        #endregion

        #region Event Hooks

        internal StompFrame HookInterceptFrame(StompFrame Frame)
        {
            return Frame;
        }

        /// <summary>
        /// For plugins who know when someone connects successfully
        /// </summary>
        /// <param name="ConnectionFrame"></param>
        internal void HookQuerySuccessfulConnect(StompFrame ConnectionFrame)
        {

        }

        /// <summary>
        /// For plugins who know when someone fails to connect
        /// </summary>
        /// <param name="ConnectionFrame"></param>
        internal void HookQueryFailedConnect(StompFrame ConnectionFrame)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ConnectionFrame"></param>
        /// <returns>
        ///     TRUE if the client should be disconnected
        /// </returns>
        internal bool HookCancelConnect(StompFrame ConnectionFrame, out string FailReason)
        {
            FailReason = "";
            return false;
        }

        #endregion

        #region Messaging

        internal void MessageReceived(ClientConnection Sender, StompMessageFrame Message)
        {
            foreach (PluginConfig Plugin in PluginConfig.Values)
            {
                if (Plugin.ActivePermissions.Contains(PluginPermissions.ReceiveMessage) &&
                    Plugin.MessageSubscriptions != null &&
                    Plugin.GetType().IsAssignableFrom(typeof(IMessageHandler)) &&
                    Plugin.MessageSubscriptions.Any(x => x.IsMatch(Message.Destination)))
                {
                    ((IMessageHandler)Plugin).ReceiveMessage(Sender, Message);
                }
            }
        }

        public void RegisterDestinationSubscriptions(IEnumerable<Regex> Subscriptions)
        {
            if (!HasPermission(Assembly.GetCallingAssembly(), PluginPermissions.ReceiveMessage))
                throw new InvalidPermissionsException(PluginPermissions.ReceiveMessage);

            PluginConfig[Assembly.GetCallingAssembly().GetName().Name].MessageSubscriptions = Subscriptions;
        }

        public void SendMessage(string Destination, StompMessageFrame Message)
        {
            if (!HasPermission(Assembly.GetCallingAssembly(), PluginPermissions.SendMessage))
                throw new InvalidPermissionsException(PluginPermissions.SendMessage);

            _Server.Send(Message, Destination);
        }

        #endregion

        public enum PluginPermissions : ulong
        {
            Load, // Whether the plugin can even be loaded

            ReceiveMessage, // Receive messages whose destinations match a given set of regexes
            SendMessage,    // Send messages to a given destination (to those clients who are subscribed to that destination) or a given client

            QueryConnect, // See connection attempts (including ALL headers)
            CancelConnect, // Reject connection attempts
            Connection, // Be notified when a connection succeeds

            ListClients,    // Get a list of all client connections at any time

            QuerySubscriptions,     // Know what a client subscribes to a destination
            QueryUnsubscriptions,   // Know when a client unsubscribes from a destination
            SubscribeClient,    // Subscribe a client to a given destination
            UnsubscribeClient,  // Unsubscribe a client from a given destination, IF this plugin subscribed them to it
            UnsubscribeClientAll, // Unsubscribe a client from a given destination regardless of how they subscribed

            SendRawFrame, // Send raw frames

            InterceptFrames, // See frames as they come in off the wire
            ModifyFrames, // Modify intercepted frames before they go on to the next stage

            DisconnectClient, // Allows for kicking clients off arbitrarily.  Implies CancelConnect.

            StopServer, // Stop the STOMP server
            EditPermissions, // Edit permissions of any plugin except this one
            LoadPlugin // Also allows unload
        }
    }
}
