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

        public int LeasedEntries { get; set; }

        public int ConnectingEntries { get; set; }

        public int ReconnectingEntries { get; set; }

        public int FaultedEntries { get; set; }

        public int InvalidatedEntries { get; set; }

        public int DisposedEntries { get; set; }

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
