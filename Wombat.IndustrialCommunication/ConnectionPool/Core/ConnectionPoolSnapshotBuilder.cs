using System;
using System.Collections.Generic;
using System.Linq;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    /// <summary>
    /// 连接池快照构建器。
    /// </summary>
    internal static class ConnectionPoolSnapshotBuilder
    {
        public static ConnectionPoolSnapshot Build(IList<ConnectionEntrySnapshot> entries)
        {
            var safeEntries = entries ?? new List<ConnectionEntrySnapshot>();
            return new ConnectionPoolSnapshot
            {
                CapturedAtUtc = DateTime.UtcNow,
                TotalEntries = safeEntries.Count,
                ReadyEntries = safeEntries.Count(t => t.State == ConnectionEntryState.Ready),
                BusyEntries = safeEntries.Count(t => t.State == ConnectionEntryState.Busy),
                DisconnectedEntries = safeEntries.Count(t => t.State == ConnectionEntryState.Disconnected),
                UnavailableEntries = safeEntries.Count(t => t.State == ConnectionEntryState.Unavailable),
                TotalActiveLeases = safeEntries.Sum(t => t.ActiveLeaseCount),
                Entries = safeEntries
            };
        }
    }
}
