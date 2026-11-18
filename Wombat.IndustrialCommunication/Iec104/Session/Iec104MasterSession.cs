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
    /// 主站会话：STARTDT、总召唤、遥控、值缓存。
    /// </summary>
    public sealed class Iec104MasterSession : Iec104SessionBase
    {
        private readonly Iec104MasterOptions _options;
        private readonly ConcurrentDictionary<int, Iec104Value> _values = new ConcurrentDictionary<int, Iec104Value>();
        private readonly SemaphoreSlim _requestGate = new SemaphoreSlim(1, 1);

        private TaskCompletionSource<bool> _startCompletion;
        private TaskCompletionSource<Iec104InterrogationResult> _interrogationCompletion;
        private TaskCompletionSource<Iec104CommandResult> _commandCompletion;
        private Iec104CommandStage _commandStage;
        private int _commandIoa;
        private DateTime _commandStartedAt;
        private HashSet<int> _interrogationSeen;
        private DateTime _interrogationStartedAt;
        private bool _interrogationConfirmed;

        public Iec104MasterSession(IIec104Link link, Iec104MasterOptions options)
            : base(link, options.K, options.W, options.T1, options.T2, options.T3, options.EnableTrace, options.StrictSequence)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public event EventHandler<Iec104ValueEventArgs> ValueReceived;

        public event EventHandler<Iec104LinkEventArgs> LinkStateChanged;

        /// <summary>建立链路并完成 STARTDT。</summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            StartLoops();
            RaiseLink(Iec104LinkState.Connected, "TCP 已连接");

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _startCompletion = completion;

            try
            {
                await SendUnnumberedAsync(ApciFunction.StartDataTransferActivation, cancellationToken).ConfigureAwait(false);
                await WaitAsync(completion.Task, T1, cancellationToken, "STARTDT 确认超时。").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RaiseLink(Iec104LinkState.Failed, "链路激活失败", ex);
                throw;
            }
            finally
            {
                _startCompletion = null;
            }
        }

        public IReadOnlyDictionary<int, Iec104Value> Snapshot()
        {
            return new Dictionary<int, Iec104Value>(_values);
        }

        public bool TryGetValue(int ioa, out Iec104Value value)
        {
            return _values.TryGetValue(ioa, out value);
        }

        /// <summary>执行一次站总召唤。</summary>
        public async Task<Iec104InterrogationResult> InterrogateAsync(CancellationToken cancellationToken)
        {
            await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var completion = new TaskCompletionSource<Iec104InterrogationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _interrogationCompletion = completion;
                _interrogationSeen = new HashSet<int>();
                _interrogationConfirmed = false;
                _interrogationStartedAt = DateTime.UtcNow;

                await SendInformationAsync(AsduBuilder.InterrogationCommand(_options.CommonAddress), cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    return await WaitAsync(completion.Task, _options.InterrogationTimeout, cancellationToken, "总召唤超时。")
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    return BuildInterrogationResult(false, true);
                }
            }
            finally
            {
                _interrogationCompletion = null;
                _requestGate.Release();
            }
        }

        /// <summary>单点遥控。</summary>
        public Task<Iec104CommandResult> SingleCommandAsync(
            int ioa,
            bool on,
            Iec104CommandMode mode,
            CancellationToken cancellationToken)
        {
            var select = Iec104InformationObject.SingleCommand(ioa, on, 0, true);
            var execute = Iec104InformationObject.SingleCommand(ioa, on, 0, false);
            return ExecuteCommandAsync(Iec104TypeId.SingleCommand, ioa, select, execute, mode, cancellationToken);
        }

        /// <summary>双点遥控。</summary>
        public Task<Iec104CommandResult> DoubleCommandAsync(
            int ioa,
            Iec104DoublePointState state,
            Iec104CommandMode mode,
            CancellationToken cancellationToken)
        {
            var select = Iec104InformationObject.DoubleCommand(ioa, state, 0, true);
            var execute = Iec104InformationObject.DoubleCommand(ioa, state, 0, false);
            return ExecuteCommandAsync(Iec104TypeId.DoubleCommand, ioa, select, execute, mode, cancellationToken);
        }

        /// <summary>
        /// 只发选择帧并等待选择确认，不发执行帧。用于桥接层的两段式确认：
        /// 上半段由 Modbus 侧请求触发，下半段由确认码触发。
        /// </summary>
        public async Task<Iec104CommandResult> SelectSingleCommandAsync(
            int ioa,
            bool on,
            CancellationToken cancellationToken)
        {
            await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var select = Iec104InformationObject.SingleCommand(ioa, on, 0, true);
                return await SendCommandAsync(
                    Iec104TypeId.SingleCommand, select, Iec104CommandStage.Select, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _requestGate.Release();
            }
        }

        private async Task<Iec104CommandResult> ExecuteCommandAsync(
            Iec104TypeId typeId,
            int ioa,
            Iec104InformationObject selectObject,
            Iec104InformationObject executeObject,
            Iec104CommandMode mode,
            CancellationToken cancellationToken)
        {
            await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (mode == Iec104CommandMode.SelectAndExecute)
                {
                    var selected = await SendCommandAsync(typeId, selectObject, Iec104CommandStage.Select, cancellationToken)
                        .ConfigureAwait(false);
                    if (!selected.Accepted) return selected;
                }

                return await SendCommandAsync(typeId, executeObject, Iec104CommandStage.Execute, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _requestGate.Release();
            }
        }

        private async Task<Iec104CommandResult> SendCommandAsync(
            Iec104TypeId typeId,
            Iec104InformationObject item,
            Iec104CommandStage stage,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<Iec104CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _commandCompletion = completion;
            _commandStage = stage;
            _commandIoa = item.Ioa;
            _commandStartedAt = DateTime.UtcNow;

            try
            {
                var asdu = AsduBuilder.Build(
                    typeId, _options.CommonAddress, Iec104Cause.Activation, new[] { item });
                await SendInformationAsync(asdu, cancellationToken).ConfigureAwait(false);

                try
                {
                    return await WaitAsync(completion.Task, _options.CommandTimeout, cancellationToken, "遥控超时。")
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    return new Iec104CommandResult
                    {
                        Ioa = item.Ioa,
                        Stage = stage,
                        TimedOut = true,
                        Cause = Iec104Cause.Activation,
                        Duration = DateTime.UtcNow - _commandStartedAt,
                        Message = "等待遥控确认超时。",
                    };
                }
            }
            finally
            {
                _commandCompletion = null;
            }
        }

        protected override void OnStarted()
        {
            var completion = _startCompletion;
            if (completion != null) completion.TrySetResult(true);
            RaiseLink(Iec104LinkState.DataTransferActive, "STARTDT 完成");
        }

        protected override void OnFault(Exception exception)
        {
            RaiseLink(Iec104LinkState.Disconnected, "链路断开", exception);

            var start = _startCompletion;
            if (start != null) start.TrySetException(exception);

            var interrogation = _interrogationCompletion;
            if (interrogation != null) interrogation.TrySetException(exception);

            var command = _commandCompletion;
            if (command != null) command.TrySetException(exception);
        }

        protected override void OnAsdu(Asdu asdu)
        {
            if (asdu.CommonAddress != 0 && asdu.CommonAddress != _options.CommonAddress)
            {
                return;
            }

            switch (asdu.TypeId)
            {
                case Iec104TypeId.InterrogationCommand:
                    HandleInterrogation(asdu);
                    return;

                case Iec104TypeId.SingleCommand:
                case Iec104TypeId.DoubleCommand:
                    HandleCommandResponse(asdu);
                    return;
            }

            var interrogated = _interrogationCompletion != null && asdu.Cause == Iec104Cause.InterrogatedByStation;
            for (var i = 0; i < asdu.Objects.Count; i++)
            {
                var item = asdu.Objects[i];
                if (!IsDataPoint(item.TypeId)) continue;

                var value = Iec104Value.FromObject(asdu.CommonAddress, item, DateTime.Now);
                _values[item.Ioa] = value;

                if (interrogated && _interrogationSeen != null)
                {
                    _interrogationSeen.Add(item.Ioa);
                }

                var handler = ValueReceived;
                if (handler != null)
                {
                    try { handler(this, new Iec104ValueEventArgs(value)); } catch { }
                }
            }
        }

        private void HandleInterrogation(Asdu asdu)
        {
            if (_interrogationCompletion == null) return;

            if (asdu.Cause == Iec104Cause.ActivationConfirmation)
            {
                _interrogationConfirmed = true;
                return;
            }

            if (asdu.Cause == Iec104Cause.ActivationTermination)
            {
                _interrogationCompletion.TrySetResult(BuildInterrogationResult(true, false));
            }
        }

        private void HandleCommandResponse(Asdu asdu)
        {
            var completion = _commandCompletion;
            if (completion == null || asdu.Objects.Count == 0) return;
            if (asdu.Objects[0].Ioa != _commandIoa) return;

            if (asdu.Cause == Iec104Cause.ActivationConfirmation && asdu.Negative)
            {
                completion.TrySetResult(new Iec104CommandResult
                {
                    Ioa = _commandIoa,
                    Accepted = false,
                    Negative = true,
                    Cause = asdu.Cause,
                    Stage = _commandStage,
                    Duration = DateTime.UtcNow - _commandStartedAt,
                    Message = "从站否定确认。",
                });
                return;
            }

            if (asdu.Cause == Iec104Cause.ActivationConfirmation && _commandStage == Iec104CommandStage.Select)
            {
                completion.TrySetResult(new Iec104CommandResult
                {
                    Ioa = _commandIoa,
                    Accepted = true,
                    Cause = asdu.Cause,
                    Stage = Iec104CommandStage.Select,
                    Duration = DateTime.UtcNow - _commandStartedAt,
                });
                return;
            }

            if (asdu.Cause == Iec104Cause.ActivationTermination)
            {
                completion.TrySetResult(new Iec104CommandResult
                {
                    Ioa = _commandIoa,
                    Accepted = true,
                    Cause = asdu.Cause,
                    Stage = Iec104CommandStage.Termination,
                    Duration = DateTime.UtcNow - _commandStartedAt,
                });
            }
        }

        private Iec104InterrogationResult BuildInterrogationResult(bool completed, bool timedOut)
        {
            var seen = _interrogationSeen ?? new HashSet<int>();
            var missing = new List<int>();
            var expected = 0;

            var table = _options.PointTable;
            if (table != null)
            {
                for (var i = 0; i < table.Points.Count; i++)
                {
                    var point = table.Points[i];
                    if (!IsDataPoint(point.TypeId)) continue;

                    expected++;
                    if (!seen.Contains(point.Ioa)) missing.Add(point.Ioa);
                }
            }

            return new Iec104InterrogationResult
            {
                Completed = completed,
                TimedOut = timedOut,
                Confirmed = _interrogationConfirmed,
                ReceivedCount = seen.Count,
                ExpectedCount = expected == 0 ? seen.Count : expected,
                MissingIoa = missing,
                Duration = DateTime.UtcNow - _interrogationStartedAt,
            };
        }

        private static bool IsDataPoint(Iec104TypeId typeId)
        {
            switch (typeId)
            {
                case Iec104TypeId.SinglePoint:
                case Iec104TypeId.DoublePoint:
                case Iec104TypeId.StepPosition:
                case Iec104TypeId.MeasuredNormalized:
                case Iec104TypeId.MeasuredScaled:
                case Iec104TypeId.MeasuredFloat:
                case Iec104TypeId.SinglePointWithTime:
                case Iec104TypeId.DoublePointWithTime:
                case Iec104TypeId.MeasuredNormalizedWithTime:
                case Iec104TypeId.MeasuredScaledWithTime:
                case Iec104TypeId.MeasuredFloatWithTime:
                    return true;
                default:
                    return false;
            }
        }

        private void RaiseLink(Iec104LinkState state, string message, Exception exception = null)
        {
            var handler = LinkStateChanged;
            if (handler == null) return;

            try { handler(this, new Iec104LinkEventArgs(state, message, exception)); } catch { }
        }

        private static async Task<T> WaitAsync<T>(
            Task<T> task,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            string message)
        {
            var delay = Task.Delay(timeout, cancellationToken);
            var completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
            if (completed != task)
            {
                throw new TimeoutException(message);
            }

            return await task.ConfigureAwait(false);
        }
    }
}
