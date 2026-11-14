using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Iec104.Diagnostics;
using Wombat.IndustrialCommunication.Iec104.Model;
using Wombat.IndustrialCommunication.Iec104.Options;
using Wombat.IndustrialCommunication.Iec104.Protocol;
using Wombat.IndustrialCommunication.Iec104.Transport;

namespace Wombat.IndustrialCommunication.Iec104.Session
{
    /// <summary>
    /// 从站会话：应答 STARTDT、总召、遥控，支持周期与突发上送。
    /// 读循环不阻塞，所有发送动作经动作锁串行化，避免 N(S) 与业务顺序错乱。
    /// </summary>
    public sealed class Iec104SlaveSession : Iec104SessionBase
    {
        private readonly Iec104SlaveOptions _options;
        private readonly Iec104DataStore _store;
        private readonly SemaphoreSlim _actionGate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<int, DateTime> _selections = new Dictionary<int, DateTime>();
        private readonly ConcurrentDictionary<int, byte> _pendingChanges = new ConcurrentDictionary<int, byte>();
        private readonly Guid _sessionId = Guid.NewGuid();

        private Task _cyclicTask;
        private int _interrogating;
        private int _flushing;

        public Iec104SlaveSession(IIec104Link link, Iec104SlaveOptions options, Iec104DataStore store)
            : base(link, options.K, options.W, options.T1, options.T2, options.T3, options.EnableTrace)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Guid SessionId
        {
            get { return _sessionId; }
        }

        public event EventHandler<Iec104CommandRequestEventArgs> CommandRequested;

        public event EventHandler<Iec104LinkEventArgs> LinkStateChanged;

        /// <summary>启动会话的读写循环与周期任务。</summary>
        public void Start()
        {
            StartLoops();
            RaiseLink(Iec104LinkState.Connected, "客户端已连接");

            if (_options.CyclicPeriod > TimeSpan.Zero)
            {
                _cyclicTask = Task.Run((Func<Task>)CyclicLoopAsync);
            }
        }

        /// <summary>值变化时通知会话，由会话合并后突发上送。</summary>
        public void NotifyValueChanged(int ioa)
        {
            _pendingChanges[ioa] = 0;

            if (Interlocked.CompareExchange(ref _flushing, 1, 0) != 0) return;
            Run(FlushChangesAsync);
        }

        protected override void OnStarted()
        {
            RaiseLink(Iec104LinkState.DataTransferActive, "STARTDT 完成");

            if (_options.SendEndOfInitialization)
            {
                Run(async () => await SendInformationAsync(
                    AsduBuilder.EndOfInitialization(_options.CommonAddress), SessionToken).ConfigureAwait(false));
            }
        }

        protected override void OnFault(Exception exception)
        {
            RaiseLink(Iec104LinkState.Disconnected, "链路断开", exception);
        }

        protected override void OnAsdu(Asdu asdu)
        {
            if (asdu.CommonAddress != 0 && asdu.CommonAddress != _options.CommonAddress) return;

            switch (asdu.TypeId)
            {
                case Iec104TypeId.InterrogationCommand:
                    if (asdu.Cause == Iec104Cause.Activation)
                    {
                        Run(InterrogateAsync);
                    }
                    else if (asdu.Cause == Iec104Cause.Deactivation)
                    {
                        Run(async () => await SendInformationAsync(
                            AsduBuilder.Build(
                                Iec104TypeId.InterrogationCommand,
                                _options.CommonAddress,
                                Iec104Cause.DeactivationConfirmation,
                                new[] { Iec104InformationObject.InterrogationResponse() }),
                            SessionToken).ConfigureAwait(false));
                    }

                    break;

                case Iec104TypeId.SingleCommand:
                case Iec104TypeId.DoubleCommand:
                    if (asdu.Cause == Iec104Cause.Activation)
                    {
                        Run(() => HandleCommandAsync(asdu));
                    }

                    break;

                case Iec104TypeId.ClockSynchronizationCommand:
                    if (asdu.Cause == Iec104Cause.Activation)
                    {
                        Run(async () => await SendInformationAsync(
                            AsduBuilder.Build(
                                Iec104TypeId.ClockSynchronizationCommand,
                                _options.CommonAddress,
                                Iec104Cause.ActivationConfirmation,
                                asdu.Objects),
                            SessionToken).ConfigureAwait(false));
                    }

                    break;
            }
        }

        private async Task InterrogateAsync()
        {
            if (Interlocked.Exchange(ref _interrogating, 1) != 0) return;

            await _actionGate.WaitAsync(SessionToken).ConfigureAwait(false);
            try
            {
                await SendInformationAsync(
                    AsduBuilder.InterrogationConfirmation(_options.CommonAddress), SessionToken).ConfigureAwait(false);

                var groups = GroupPoints();
                foreach (var group in groups)
                {
                    var objects = BuildObjects(group.Value);
                    if (objects.Count == 0) continue;

                    var frames = AsduBuilder.BuildFrames(
                        group.Key, _options.CommonAddress, Iec104Cause.InterrogatedByStation, objects);

                    for (var i = 0; i < frames.Count; i++)
                    {
                        await SendInformationAsync(frames[i], SessionToken).ConfigureAwait(false);
                    }
                }

                await SendInformationAsync(
                    AsduBuilder.InterrogationTermination(_options.CommonAddress), SessionToken).ConfigureAwait(false);
            }
            finally
            {
                _actionGate.Release();
                Interlocked.Exchange(ref _interrogating, 0);
            }
        }

        private async Task CyclicLoopAsync()
        {
            try
            {
                while (!SessionToken.IsCancellationRequested)
                {
                    await Task.Delay(_options.CyclicPeriod, SessionToken).ConfigureAwait(false);
                    if (!DataTransferActive) continue;

                    await SendAllAsync(Iec104Cause.Periodic).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Fault(ex);
            }
        }

        private async Task SendAllAsync(Iec104Cause cause)
        {
            await _actionGate.WaitAsync(SessionToken).ConfigureAwait(false);
            try
            {
                if (Volatile.Read(ref _interrogating) != 0 && _options.PauseUnsolicitedDuringInterrogation) return;

                var groups = GroupPoints();
                foreach (var group in groups)
                {
                    var objects = BuildObjects(group.Value);
                    if (objects.Count == 0) continue;

                    var frames = AsduBuilder.BuildFrames(
                        group.Key, _options.CommonAddress, cause, objects);

                    for (var i = 0; i < frames.Count; i++)
                    {
                        await SendInformationAsync(frames[i], SessionToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _actionGate.Release();
            }
        }

        private async Task FlushChangesAsync()
        {
            try
            {
                while (!SessionToken.IsCancellationRequested)
                {
                    if (_pendingChanges.IsEmpty)
                    {
                        Interlocked.Exchange(ref _flushing, 0);
                        if (_pendingChanges.IsEmpty) return;
                        if (Interlocked.CompareExchange(ref _flushing, 1, 0) != 0) return;
                    }

                    await _actionGate.WaitAsync(SessionToken).ConfigureAwait(false);
                    List<int> batch;
                    try
                    {
                        if (Volatile.Read(ref _interrogating) != 0 && _options.PauseUnsolicitedDuringInterrogation)
                        {
                            await Task.Delay(50, SessionToken).ConfigureAwait(false);
                            continue;
                        }

                        batch = new List<int>(_pendingChanges.Keys);
                        for (var i = 0; i < batch.Count; i++) _pendingChanges.TryRemove(batch[i], out _);
                    }
                    finally
                    {
                        _actionGate.Release();
                    }

                    for (var i = 0; i < batch.Count; i++)
                    {
                        await SendPointAsync(batch[i], Iec104Cause.Spontaneous).ConfigureAwait(false);
                    }

                    await Task.Delay(20, SessionToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Fault(ex);
            }
        }

        private async Task SendPointAsync(int ioa, Iec104Cause cause)
        {
            Iec104Point point;
            if (_store.Table == null || !_store.Table.TryGetPoint(ioa, out point)) return;
            if (point.IsCommand) return;

            Iec104Value value;
            if (!_store.TryGet(ioa, out value)) return;

            var item = _store.BuildObject(point, value);
            if (item == null) return;

            await SendInformationAsync(
                AsduBuilder.Build(point.TypeId, _options.CommonAddress, cause, new[] { item }),
                SessionToken).ConfigureAwait(false);
        }

        private async Task HandleCommandAsync(Asdu asdu)
        {
            if (asdu.Objects.Count == 0) return;
            var item = asdu.Objects[0];
            var isSelect = (item.Element[0] & 0x80) != 0;

            var single = asdu.TypeId == Iec104TypeId.SingleCommand ? (bool?)((item.Element[0] & 0x01) != 0) : null;
            var doubleState = asdu.TypeId == Iec104TypeId.DoubleCommand
                ? (Iec104DoublePointState?)(Iec104DoublePointState)(item.Element[0] & 0x03)
                : null;

            var request = new Iec104CommandRequestEventArgs(item.Ioa, isSelect, single, doubleState);
            var handler = CommandRequested;
            if (handler != null)
            {
                try { handler(this, request); } catch { }
            }

            var accepted = !request.Cancel && IsCommandPoint(item.Ioa);

            if (accepted && !isSelect && _options.RequireSelectBeforeOperate)
            {
                accepted = HasValidSelection(item.Ioa);
            }

            await _actionGate.WaitAsync(SessionToken).ConfigureAwait(false);
            try
            {
                await SendInformationAsync(
                    AsduBuilder.Build(
                        asdu.TypeId,
                        _options.CommonAddress,
                        Iec104Cause.ActivationConfirmation,
                        new[] { item },
                        negative: !accepted),
                    SessionToken).ConfigureAwait(false);

                if (!accepted) return;

                if (isSelect)
                {
                    lock (_selections) _selections[item.Ioa] = DateTime.UtcNow;
                    return;
                }

                lock (_selections) _selections.Remove(item.Ioa);

                await SendInformationAsync(
                    AsduBuilder.Build(
                        asdu.TypeId,
                        _options.CommonAddress,
                        Iec104Cause.ActivationTermination,
                        new[] { item }),
                    SessionToken).ConfigureAwait(false);
            }
            finally
            {
                _actionGate.Release();
            }
        }

        private bool IsCommandPoint(int ioa)
        {
            Iec104Point point;
            return _store.Table != null
                && _store.Table.TryGetPoint(ioa, out point)
                && point.IsCommand;
        }

        private bool HasValidSelection(int ioa)
        {
            lock (_selections)
            {
                DateTime selectedAt;
                if (!_selections.TryGetValue(ioa, out selectedAt)) return false;
                if (DateTime.UtcNow - selectedAt > _options.SelectTimeout)
                {
                    _selections.Remove(ioa);
                    return false;
                }

                return true;
            }
        }

        private Dictionary<Iec104TypeId, List<Iec104Point>> GroupPoints()
        {
            var groups = new Dictionary<Iec104TypeId, List<Iec104Point>>();
            var table = _store.Table;
            if (table == null) return groups;

            for (var i = 0; i < table.Points.Count; i++)
            {
                var point = table.Points[i];
                if (point.IsCommand) continue;
                if (!IsSupportedDataType(point.TypeId)) continue;

                List<Iec104Point> list;
                if (!groups.TryGetValue(point.TypeId, out list))
                {
                    list = new List<Iec104Point>();
                    groups.Add(point.TypeId, list);
                }

                list.Add(point);
            }

            return groups;
        }

        private List<Iec104InformationObject> BuildObjects(List<Iec104Point> points)
        {
            var objects = new List<Iec104InformationObject>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                Iec104Value value;
                if (!_store.TryGet(points[i].Ioa, out value)) continue;

                var item = _store.BuildObject(points[i], value);
                if (item != null) objects.Add(item);
            }

            return objects;
        }

        private static bool IsSupportedDataType(Iec104TypeId typeId)
        {
            switch (typeId)
            {
                case Iec104TypeId.SinglePoint:
                case Iec104TypeId.SinglePointWithTime:
                case Iec104TypeId.DoublePoint:
                case Iec104TypeId.DoublePointWithTime:
                case Iec104TypeId.MeasuredFloat:
                case Iec104TypeId.MeasuredFloatWithTime:
                    return true;
                default:
                    return false;
            }
        }

        private void Run(Func<Task> action)
        {
            Task.Run(async () =>
            {
                try
                {
                    await action().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    Fault(ex);
                }
            });
        }

        private void RaiseLink(Iec104LinkState state, string message, Exception exception = null)
        {
            var handler = LinkStateChanged;
            if (handler == null) return;

            try { handler(this, new Iec104LinkEventArgs(state, message, exception)); } catch { }
        }

        /// <summary>停止会话，额外等待周期任务退出。</summary>
        public override async Task StopAsync()
        {
            await base.StopAsync().ConfigureAwait(false);

            var cyclic = _cyclicTask;
            if (cyclic != null)
            {
                try { await cyclic.ConfigureAwait(false); } catch { }
            }

            _actionGate.Dispose();
        }
    }
}
