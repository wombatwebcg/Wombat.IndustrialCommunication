using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Iec104.Diagnostics;
using Wombat.IndustrialCommunication.Iec104.Model;

namespace Wombat.IndustrialCommunication.Iec104.ModbusBridge
{
    /// <summary>
    /// 把 <see cref="Iec104Master"/> 适配成桥接层的数据来源。
    /// 只做事件转发与状态缓存，不改变 104 侧任何行为。
    /// </summary>
    public sealed class Iec104MasterValueSource : IIec104ValueSource, IIec104CommandSink, IDisposable
    {
        private readonly Iec104Master _master;
        private Iec104LinkState _linkState;
        private int _disposed;

        public Iec104MasterValueSource(Iec104Master master)
        {
            _master = master ?? throw new ArgumentNullException(nameof(master));
            _linkState = master.IsConnected ? Iec104LinkState.DataTransferActive : Iec104LinkState.Disconnected;

            _master.ValueReceived += OnValueReceived;
            _master.LinkStateChanged += OnLinkStateChanged;
        }

        public Iec104PointTable PointTable
        {
            get { return _master.Options.PointTable; }
        }

        public Iec104LinkState LinkState
        {
            get { return _linkState; }
        }

        public event EventHandler<Iec104ValueEventArgs> ValueChanged;

        public event EventHandler<Iec104LinkEventArgs> LinkStateChanged;

        public bool TryGetValue(int ioa, out Iec104Value value)
        {
            return _master.TryGetValue(ioa, out value);
        }

        public IReadOnlyDictionary<int, Iec104Value> Snapshot()
        {
            return _master.Snapshot();
        }

        public Task<Iec104CommandResult> SelectSingleCommandAsync(
            int ioa,
            bool on,
            CancellationToken cancellationToken)
        {
            return _master.SelectSingleCommandAsync(ioa, on, cancellationToken);
        }

        public Task<Iec104CommandResult> SingleCommandAsync(
            int ioa,
            bool on,
            Iec104CommandMode mode,
            CancellationToken cancellationToken)
        {
            return _master.SingleCommandAsync(ioa, on, mode, cancellationToken);
        }

        private void OnValueReceived(object sender, Iec104ValueEventArgs e)
        {
            var handler = ValueChanged;
            if (handler != null)
            {
                try { handler(this, e); } catch { }
            }
        }

        private void OnLinkStateChanged(object sender, Iec104LinkEventArgs e)
        {
            _linkState = e.State;

            var handler = LinkStateChanged;
            if (handler != null)
            {
                try { handler(this, e); } catch { }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            _master.ValueReceived -= OnValueReceived;
            _master.LinkStateChanged -= OnLinkStateChanged;
            ValueChanged = null;
            LinkStateChanged = null;
        }
    }
}
