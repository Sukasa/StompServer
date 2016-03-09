using STOMP.Frames;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using STOMP.Server.Clients;
using System.Security;
using System.Security.Policy;
using System.Security.Permissions;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;

namespace STOMP.Server.Plugins
{
    public class PluginInterface
    {
        private STOMPServer _Server;
        private Dictionary<string, PluginSettings> _PluginConfig;

        public string PluginBasePath { get; set; }

        /* 
                * Some notes regarding plugin loading...
                * I should use a separate AppDomain for each plugin, and use a proxy.  This will likely impact performance a bit, but it wil give me the ability to load and *unload* plugins dynamically
                * Also, how to bind plugin hooks?
                *  
                */

        private AppDomain CreatePluginAppDomain(string PluginFriendlyName)
        {
            PermissionSet PermSet = new PermissionSet(PermissionState.None);
            PermSet.AddPermission(new FileIOPermission(FileIOPermissionAccess.Read | FileIOPermissionAccess.Write | FileIOPermissionAccess.Append, PluginBasePath + Path.DirectorySeparatorChar + PluginFriendlyName));
            PermSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));

            StrongName ServerAssembly = typeof(PluginInterface).Assembly.Evidence.GetHostEvidence<StrongName>();

            AppDomainSetup DomainSetup = new AppDomainSetup();

            DomainSetup.ApplicationBase = Path.GetFullPath("Plugins");
            DomainSetup.DisallowBindingRedirects = false;
            DomainSetup.DisallowCodeDownload = true;
            DomainSetup.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

            AppDomain Domain = AppDomain.CreateDomain("PluginDomain_" + PluginFriendlyName, null, DomainSetup, PermSet, ServerAssembly);

            return Domain;
        }

        private bool DestroyPluginAppDomain(AppDomain Domain)
        {
            try
            {
                AppDomain.Unload(Domain);
            }
            catch (CannotUnloadAppDomainException ex)
            {
                Console.WriteLine("Failed to destroy domain " + Domain.FriendlyName);
                Console.WriteLine(ex.Message.ToString());

                // Failed to destroy domain - could be a plugin trying to play dirty
                // TODO do something about it
                return false;
            }
            return true;
        }

        internal PluginInterface(STOMPServer Server)
        {
            _Server = Server;
            _PluginConfig = new Dictionary<string, PluginSettings>();

            // TODO Load Plugins here
        }

        public bool SendRawFrame(StompFrame Frame)
        {
            // TODO send raw frame to client
            return false;
        }

        public bool StopServer()
        {
            // TODO stop server
            return false;
        }

        #region Plugin Management

        public void SetPermission(PluginSettings Plugin, PluginPermissions Permission, bool PermissionSetting)
        {
            // TODO set plugin permission
        }

        internal PluginPermissions[] RegisterForEvents(Type PluginClass)
        {
            // TODO Register plugin for events 

            // Check permissions list for this plugin


            // Register for permissions that are marked YES


            // Add any missing permissions strings to the list, set to NO



            return null;
        }

        public void LoadPlugin(PluginSettings Plugin)
        {
            // TODO Load plugin
            Plugin.Domain = CreatePluginAppDomain(Plugin.FriendlyName);
            


            Plugin.Domain.Load(Plugin.QualifiedName);
            Plugin.Domain.CreateInstanceAndUnwrap(Plugin.QualifiedName, Plugin.FileName);
        }

        public bool UnloadPlugin(PluginSettings Plugin)
        {
            Plugin.Loaded = false;
            Plugin.MessageSubscriptions = null;
            if (DestroyPluginAppDomain(Plugin.Domain))
            {
                Plugin.Domain = null;
                return true;
            }
            return false;
        }

        public IEnumerable<PluginSettings> GetPlugins()
        {
            // TODO get current plugins
            return null;
        }

        public bool HasPermission(Assembly Plugin, PluginPermissions Permission)
        {
            return _PluginConfig.ContainsKey(Plugin.GetName().Name) && _PluginConfig[Plugin.GetName().Name].ActivePermissions.Contains(Permission);
        }

        public void LoadConfig()
        {

        }

        #endregion

        #region Event Hooks

        internal bool HookInterceptIncomingFrame(ClientConnection Client, StompFrame Frame)
        {
            // TODO intercept frames

            return false;
        }

        internal bool HookInterceptOutgoingFrame(ClientConnection Client, StompFrame Frame)
        {
            // TODO intercept frames

            return false;
        }

        /// <summary>
        /// For plugins who know when someone connects successfully
        /// </summary>
        /// <param name="ConnectionFrame"></param>
        internal void HookQuerySuccessfulConnect(StompFrame ConnectionFrame)
        {
            // TODO notify plugins of successful connection
        }

        /// <summary>
        /// For plugins who know when someone fails to connect
        /// </summary>
        /// <param name="ConnectionFrame"></param>
        internal void HookQueryFailedConnect(StompFrame ConnectionFrame)
        {
            // TODO notify plugins of failed connection
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
            // TODO ask plugins if connection should be failed
            FailReason = "";
            return false;
        }

        internal void HookAcknowledgeReceived(StompAckFrame AckFrame)
        {
            // TODO Notify plugins of frame (N)Ack
        }

        #endregion

        #region Messaging

        internal void MessageReceived(ClientConnection Sender, StompMessageFrame Message)
        {
            foreach (PluginSettings Plugin in _PluginConfig.Values)
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

            _PluginConfig[Assembly.GetCallingAssembly().GetName().Name].MessageSubscriptions = Subscriptions;
        }

        public void SendMessage(string Destination, StompMessageFrame Message)
        {
            if (!HasPermission(Assembly.GetCallingAssembly(), PluginPermissions.SendMessage))
                throw new InvalidPermissionsException(PluginPermissions.SendMessage);

            // TODO send to each individual client
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
