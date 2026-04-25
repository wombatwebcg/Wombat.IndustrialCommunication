using System;
using System.Collections.Generic;

namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 连接池整体快照。
    /// </summary>
    public class ConnectionPoolSnapshot
    {
        public int TotalEntries { get; set; }

        public int ReadyEntries { get; set; }

        public int BusyEntries { get; set; }

        public int DisconnectedEntries { get; set; }

        public int UnavailableEntries { get; set; }

        public int TotalActiveLeases { get; set; }

        public DateTime CapturedAtUtc { get; set; }

        public IList<ConnectionEntrySnapshot> Entries { get; set; }

        public ConnectionPoolSnapshot()
        {
            CapturedAtUtc = DateTime.UtcNow;
            Entries = new List<ConnectionEntrySnapshot>();
        }
    }
}
