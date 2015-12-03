using STOMP;
using STOMP.Frames;
using STOMP.Server.Clients;
using STOMP.Server.Plugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace STOMP.Server
{
    class STOMPServer
    {
        // Networking
        private TcpListener _Listener;
        private ConcurrentDictionary<string, ClientConnection> _Connections = new ConcurrentDictionary<string, ClientConnection>();

        // I/O support (Multithreaded)
        private ConcurrentQueue<Tuple<string, StompFrame>> Outbox = new ConcurrentQueue<Tuple<string, StompFrame>>();
        private BlockingCollection<Tuple<string, StompFrame>> OutboxController;
        private ConcurrentQueue<Tuple<ClientConnection, StompFrame>> Inbox = new ConcurrentQueue<Tuple<ClientConnection, StompFrame>>();
        private BlockingCollection<Tuple<ClientConnection, StompFrame>> InboxController;

        // Plugin Manager
        private PluginInterface _PluginManager;
        internal IDictionary<string, Type> FrameTypeMapping = new Dictionary<string, Type>();

        public PluginInterface Plugins
        {
            get
            {
                return _PluginManager;
            }
        }

        public string Version()
        {
            FileVersionInfo Version = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);

            return String.Format("Pug/{0}.{1}.{2}", Version.FileMajorPart, Version.FileMinorPart, Version.FileBuildPart);
        }

        internal void AddToInbox(StompFrame Frame, ClientConnection Sender)
        {
            InboxController.Add(new Tuple<ClientConnection, StompFrame>(Sender, Frame));
        }

        internal void Send(StompFrame Frame, String Destination)
        {
            OutboxController.Add(new Tuple<string, StompFrame>(Destination, Frame));
        }

        internal void RemoveClient(ClientConnection Client)
        {
            // TODO notify plugins

            ClientConnection RC;

            while (!_Connections.TryRemove(Client.MyId, out RC))
                ;
        }

        private void InboxThread()
        {
            while (true)
            {
                Tuple<ClientConnection, StompFrame> InboxItem = InboxController.Take();

                StompFrame Frame = InboxItem.Item2;

                if (Frame is StompMessageFrame)
                {
                    _PluginManager.MessageReceived(InboxItem.Item1, (StompMessageFrame)Frame);
                }
            }
        }

        private void OutboxThread()
        {
            while (true)
            {
                Tuple<string, StompFrame> Message = OutboxController.Take();

                foreach (ClientConnection C in _Connections.Values)
                {
                    C.Send(Message.Item2, Message.Item1);
                }
            }
        }

        private void Run()
        {
            // Load plugins
            _PluginManager = new PluginInterface(this);

            InboxController = new BlockingCollection<Tuple<ClientConnection, StompFrame>>(Inbox);
            OutboxController = new BlockingCollection<Tuple<string, StompFrame>>(Outbox);

            _Listener = new TcpListener(IPAddress.Parse("0.0.0.0"), 80);
            _Listener.Start();
            int SleepTime = 1000;
            int LastSleepTime = 0;

            while (true)
            {
                while (_Listener.Pending())
                {
                    TcpClient NewClient = _Listener.AcceptTcpClient();
                    ClientConnection Client = new ClientConnection(NewClient, this);

                    while (!_Connections.TryAdd(Client.MyId, Client))
                        ;
                }

                LastSleepTime = SleepTime;
                SleepTime = 1000;

                foreach (ClientConnection Connection in _Connections.Values)
                {
                    int Time = Connection.Tick(LastSleepTime);

                    if (SleepTime < Time)
                        SleepTime = Time;
                }

                Thread.Sleep(SleepTime);
            }
        }
    }
}
