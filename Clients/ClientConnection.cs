using STOMP.Frames;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace STOMP.Server.Clients
{
    public class ClientConnection : IDisposable
    {
        public IList<ClientSubscription> Subscriptions { get; private set; }
        private IDictionary<string, Queue<StompFrame>> TransactionQueue;

        private IDictionary<string, Tuple<ClientSubscription, RequiredAck>> AckIdToData;

        private static long _MessageId;
        private static long _Ack;
        private static long _UniqueId;

        private int _HeartbeatRXInterval;
        private int _HeartbeatRXCountdown;

        private int _HeartbeatTXInterval;
        private int _HeartbeatTXCountdown;

        private string _MyId;
        private TcpClient _ClientConnection;
        private ConcurrentQueue<StompFrame> _Outbox;

        private StompRingBuffer<byte> _InBuffer;
        private STOMPServer _Server;
        private ConnectionState _State;

        internal string MyId
        {
            get
            {
                return _MyId;
            }
        }

        public enum ConnectionState
        {
            AwaitingConnectFrame,
            Connected,
            Errored,
            Disconnected
        }


        internal void SendMessage(StompMessageFrame Frame, string Destination)
        {
            Frame.Destination = Destination;

            foreach (ClientSubscription CS in Subscriptions)
            {
                if (!CS.Filter.IsMatch(Destination))
                    continue;

                Frame.SubscriptionId = CS.Id;
                Frame.MessageId = Interlocked.Increment(ref _MessageId).ToString();

                if (CS.AckType != ClientSubscription.AcknowledgementType.None)
                {
                    long Ack = Interlocked.Increment(ref _Ack);
                    Frame.Ack = Ack.ToString();
                    RequiredAck RA = new RequiredAck()
                    {
                        AckId = Frame.Ack,
                        AckNum = Ack,
                        Frame = Frame,
                        HasTransactedAck = false,
                        SubscriptionId = CS.Id,
                        FrameSent = DateTime.Now
                    };
                    CS.FramesAwaitingAck.Add(RA);
                    AckIdToData.Add(Frame.Ack, new Tuple<ClientSubscription, RequiredAck>(CS, RA));
                }

                _Outbox.Enqueue(Frame);
            }
        }

        internal void Send(StompFrame Frame, string Destination = null)
        {

            if (Frame is StompMessageFrame)
            {
                if (Destination == null)
                    return; // Can't send a message frame that doesn't have a destination

                SendMessage((StompMessageFrame)Frame, Destination);
                return;
            }

            _Outbox.Enqueue(Frame);
        }

        internal void DoConnection(StompConnectFrame Frame)
        {
            // Plugin hooks for allowing/blocking connections
            string FailReason = "";
            if (_Server.Plugins.HookCancelConnect(Frame, out FailReason))
            {
                Error("Connect Failed", FailReason);
                _Server.Plugins.HookQueryFailedConnect(Frame);
                return;
            }
            _State = ConnectionState.Connected;
            bool Is12 = Frame.Version.Contains("1.2");

            if (!Is12)
            {
                Error("Connect Failed", "This server only supports version 1.2 of the STOMP protocol");
                _Server.Plugins.HookQueryFailedConnect(Frame);
                return;
            }

            _Server.Plugins.HookQuerySuccessfulConnect(Frame);

            int[] Heartbeat = Frame.Heartbeat.Split(',').Select(x => int.Parse(x)).ToArray();

            if (Heartbeat[0] > 0)
                Heartbeat[0] = Math.Max(Heartbeat[0], 1000);
            if (Heartbeat[1] > 0)
                Heartbeat[1] = Math.Max(Heartbeat[1], 1000);

            _HeartbeatRXInterval = (int)(Heartbeat[0] * 1.5);
            _HeartbeatTXInterval = (int)(Heartbeat[1] * 1.5);

            StompConnectedFrame ConFrame = new StompConnectedFrame()
            {
                Heartbeat = string.Format("{0},{1}", Heartbeat[1], Heartbeat[0]),
                Session = MyId,
                Version = "1.2",
                ServerInfo = _Server.Version()
            };

            Send(ConFrame);
        }

        // Now handle 
        internal void HandleFrame(StompFrame Frame)
        {
            switch (_State)
            {
                case ConnectionState.AwaitingConnectFrame:
                    if (Frame is StompConnectFrame) // Also catches the Stomp frame
                    {
                        DoConnection((StompConnectFrame)Frame);
                    }
                    else
                    {
                        Error("Not Connected", "You need to connect to the server with a STOMP or CONNECT frame before sending any other frames");
                    }
                    break;
                case ConnectionState.Connected:
                    if (Frame is StompDisconnectFrame)
                    {
                        if (Frame.Receipt != null)
                        {
                            StompReceiptFrame RF = new StompReceiptFrame();
                            RF.ReceiptId = Frame.Receipt;
                            Send(RF);
                        }
                        Disconnect();
                    }
                    else
                    {
                        if (Frame.TransactionId != null)
                        {
                            // TODO Transactions
                            if (Frame is StompCommitFrame)
                            {
                                // Push transaction queue to inbox
                                // Except for ACK/NACK frames - just clear the ACK list with those
                            }
                            else if (Frame is StompBeginFrame)
                            {
                                // Create new transaction queue
                            }
                            else if (Frame is StompAbortFrame)
                            {
                                // Dump transaction queue
                                // For any ACKs or NACKs, mark off the matching frame awaiting ACK/NACK that an ACK/NACK has not been recieved
                            }
                            else
                            {
                                // Append to transaction queue
                                // If this is an ACK or NACK, mark off the matching frame awaiting ACK/NACK that the reply is queued in a transaction to avoid spurious sends
                            }
                        }
                        else
                        {
                            if (Frame is StompAckFrame)
                            {
                                // Mark off the Ack
                            }
                            _Server.AddToInbox(Frame, this);
                        }
                    }
                    break;
            }
            if (Frame.Receipt != null && _State == ConnectionState.Connected)
            {
                StompReceiptFrame RF = new StompReceiptFrame();
                RF.ReceiptId = Frame.Receipt;
                Send(RF);
            }
        }


        private void ProcessAck(StompAckFrame Frame)
        {
            // Mark a frame off
            ClientSubscription CS = AckIdToData[Frame.Id].Item1;
            RequiredAck RA = AckIdToData[Frame.Id].Item2;
            switch (CS.AckType)
            {
                case ClientSubscription.AcknowledgementType.Client:
                    foreach (RequiredAck RA2 in CS.FramesAwaitingAck.Where(x => x.AckNum <= RA.AckNum))
                    {
                        CS.FramesAwaitingAck.Remove(RA2);
                        AckIdToData.Remove(RA2.AckId);
                        //_Server.Plugins.HookFrameAcknowledged(RA2.Frame);
                    }
                    break;
                case ClientSubscription.AcknowledgementType.ClientIndividual:
                    CS.FramesAwaitingAck.Remove(RA);
                    AckIdToData.Remove(Frame.Id);
                    //_Server.Plugins.HookFrameAcknowledged(Frame);
                    break;
            }
        }

        internal int Tick(int IntervalTime)
        {
            // Read
            if (_ClientConnection.Available > 0)
            {
                byte[] Data = new byte[Math.Min(_ClientConnection.Available, _InBuffer.AvailableWrite)];
                int Amt = _ClientConnection.GetStream().Read(Data, 0, Data.Length);
                _InBuffer.Write(Data, Amt);
                _HeartbeatRXCountdown = _HeartbeatRXInterval;
            }

            if (_HeartbeatRXCountdown < 0)
            {
                Error("No Heartbeat", "No data was recieved in the required time period");
                return int.MaxValue;
            }

            // Advance through any heartbeats rx'd or frame separators
            while (_InBuffer.Peek() == '\r' || _InBuffer.Peek() == '\n' || _InBuffer.Peek() == '\0')
                _InBuffer.Read(1);

            // Now try to build + dispatch the packet
            if (!TryBuildPacket(_InBuffer) && _InBuffer.AvailableWrite == 0)
            {
                Error("Frame Too Large", "The frame transmitted to the server was too large or the frames transmitted are corrupt");
                return int.MaxValue;
            }

            // Read in any other frames that arrived
            while (TryBuildPacket(_InBuffer))
                ;

            // Check for any frames that have not been ACK'd, are not transaction-queued, and are stale


            // Send all queued frames
            StompFrame Frame = null;
            while (_Outbox.TryDequeue(out Frame))
            {
                // TODO add sent frames to Ack-needed list
                Write(Frame.Serialize());
            }
            _ClientConnection.GetStream().Flush();

            return _HeartbeatRXCountdown;
        }

        internal void Write(byte[] Data)
        {
            lock (_ClientConnection.GetStream())
            {
                _ClientConnection.GetStream().Write(Data, 0, Data.Length);
            }
        }

        internal void Error(string ErrorText, string LongDesc = null)
        {
            StompErrorFrame Frame = new StompErrorFrame();
            Frame.ErrorMessage = ErrorText;
            Frame.BodyText = LongDesc;

            Write(Frame.Serialize());

            Disconnect();
            _State = ConnectionState.Errored;
        }

        internal void Disconnect()
        {
            _ClientConnection.GetStream().Flush();
            _ClientConnection.Close();

            _State = ConnectionState.Disconnected;
            _Server.RemoveClient(this);

            ((IDisposable)_ClientConnection).Dispose();
            _ClientConnection = null;
        }

        /// <summary>
        ///     Tries to build a packet from the given ringbuffer
        /// </summary>
        /// <param name="Buffer">
        ///     The Ringbuffer to build a packet from
        /// </param>
        /// <returns>
        ///     Whether it was able to build a packet or not
        /// </returns>
        private bool TryBuildPacket(StompRingBuffer<byte> Buffer)
        {
            // See if we have rx'd a packet separator or a \0 in a binary frame body
            int PacketLength = Buffer.DistanceTo(0);

            // We have, so what did we find?
            if (PacketLength > 0)
            {
                // This is a really messy block of code.

                // The goal is that it tries to determine whether it has a full packet or needs to wait for more data
                // before building the packet and dispatching it

                byte[] Data = Buffer.Peek(PacketLength);
                string Header = Encoding.UTF8.GetString(Data);
                string[] HeaderCheck = Header.Split('\n');
                int ContentLength = 0;
                bool HasContentLength = false;

                // First, we look to see if our "packet" has a content-length header.  Since we scanned out to a null (\0) byte, we're guaranteed to at least have the headers
                // of whatever packet we're examining

                for (int i = 0; i < HeaderCheck.Length && HeaderCheck[i] != "" && HeaderCheck[i] != "\r"; i++)
                {
                    // We found a content-length header?  Flag it and store how large in bytes the content should be
                    if (HeaderCheck[i].StartsWith("content-length:"))
                    {
                        HasContentLength = true;
                        ContentLength = int.Parse(HeaderCheck[i].Substring(15));
                    }
                }
                StompFrame Frame = null;

                if (HasContentLength)
                {
                    // We have a content-length header.  We need to find the start of the frame body, in bytes,
                    // and then make sure we have (ContentLength) bytes available after that

                    // Look for the end of the headers, either 1.0/1.1 or 1.2 (\r\n)-friendly
                    int EndOfHeaders = Header.IndexOf("\r\n\r\n") + 4;
                    if (EndOfHeaders == 3) // (-1) + 4
                        EndOfHeaders = Header.IndexOf("\n\n") + 2;

                    // Get the byte length of the header
                    int Offset = Encoding.UTF8.GetByteCount(Header.Substring(0, EndOfHeaders));

                    // Now see if we have that many bytes available in the ring buffer (realistically, we should except for obscene frame sizes)
                    if (Offset + ContentLength <= Buffer.AvailableRead)
                    {
                        // If we do, peek the exact packet length we want and assemble
                        Frame = StompFrame.Build(Buffer.Peek(Offset + ContentLength), _Server.FrameTypeMapping);
                        Buffer.Seek(Offset + ContentLength);
                        HandleFrame(Frame);

                        return true;
                    }
                }
                else // No content-length.  We're guaranteed to be a text packet without any overshoot; no special treatment needed
                {
                    Frame = StompFrame.Build(Data, _Server.FrameTypeMapping);
                    Buffer.Seek(PacketLength);
                    HandleFrame(Frame);

                    return true;
                }
            }

            return false;
        }

        internal ClientConnection(TcpClient Connection, STOMPServer Server)
        {
            Subscriptions = new List<ClientSubscription>();

            _Server = Server;
            _Outbox = new ConcurrentQueue<StompFrame>();
            _InBuffer = new StompRingBuffer<byte>(65536);
            _ClientConnection = Connection;

            _MyId = Interlocked.Increment(ref _UniqueId).ToString();
        }

        void IDisposable.Dispose()
        {
            if (_ClientConnection != null)
            {
                ((IDisposable)_ClientConnection).Dispose();
            }
        }
    }
}
