using STOMP.Frames;
using STOMP.Server.Clients;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STOMP.Server
{
    public abstract class Directory
    {
        public abstract bool AllowConnect(ClientConnection Client);
        public abstract void ClientConnected(ClientConnection Client);
        public abstract void ClientDisconnected(ClientConnection Client);

        public abstract IEnumerable<string> GetDirectoryIndex(string Filter); // Filter operation is determined by implementing class?

        public abstract DirectoryEntry GetDirectoryListing(string DirectoryEntryKey);

        public class DirectoryEntry
        {
            internal string _DirectoryEntryKey;
            internal string _FriendlyName;
            internal bool _Reserved;
            internal string _UniqueIdentifier;

            public DirectoryEntry()
            {

            }

            public DirectoryEntry(string EntryKey, string FriendlyName, bool IsReserved, string Identifier)
            {
                _DirectoryEntryKey = EntryKey;
                _FriendlyName = FriendlyName;
                _Reserved = IsReserved;
                _UniqueIdentifier = Identifier;
            }

            public DirectoryEntry(string EntryKey, string FriendlyName, bool IsReserved)
                : this(EntryKey, FriendlyName, IsReserved, Guid.NewGuid().ToString())
            {

            }

            public DirectoryEntry(string EntryKey, string FriendlyName)
                : this(EntryKey, FriendlyName, false, Guid.NewGuid().ToString())
            {

            }

            public DirectoryEntry(string EntryKey)
                : this(EntryKey, EntryKey, false, Guid.NewGuid().ToString())
            {

            }
        }
    }
}
