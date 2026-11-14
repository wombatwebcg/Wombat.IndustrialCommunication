using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Iec104.Diagnostics;
using Wombat.IndustrialCommunication.Iec104.Protocol;
using Wombat.IndustrialCommunication.Iec104.Transport;

namespace Wombat.IndustrialCommunication.Iec104.Session
{
    /// <summary>
    /// 会话基类：序号、k/w 窗口、t1/t2/t3、U 帧处理与读写循环。
    /// 所有出站帧经写锁串行化，N(S) 在写锁内递增。
    /// </summary>
    public abstract class Iec104SessionBase : IDisposable
    {
        private const int TickMilliseconds = 100;

        private readonly IIec104Link _link;
        private readonly Iec104FrameParser _parser = new Iec104FrameParser();
        private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private Task _readTask;
        private Task _timerTask;

        private ushort _sendSequence;
        private ushort _receiveSequence;
        private ushort _acknowledgedSequence;
        private int _unacked;
        private int _receivedSinceAck;
        private long _firstUnackedReceiveTicks;
        private long _lastReceiveTicks;
        private long _lastSendTicks;
        private long _lastAckProgressTicks;
        private long _testSentTicks;
        private int _testPending;
        private int _faulted;
        private int _stopped;
        private int _sentFrames;
        private int _receivedFrames;

        protected Iec104SessionBase(
            IIec104Link link,
            int k,
            int w,
            TimeSpan t1,
            TimeSpan t2,
            TimeSpan t3,
            bool enableTrace)
        {
            _link = link ?? throw new ArgumentNullException(nameof(link));
            if (k < 1 || k > 32767) throw new ArgumentOutOfRangeException(nameof(k));
            if (w < 1 || w > 32767) throw new ArgumentOutOfRangeException(nameof(w));

            K = k;
            W = w;
            T1 = t1;
            T2 = t2;
            T3 = t3;
            EnableTrace = enableTrace;
        }

        public int K { get; }

        public int W { get; }

        public TimeSpan T1 { get; }

        public TimeSpan T2 { get; }

        public TimeSpan T3 { get; }

        public bool EnableTrace { get; }

        /// <summary>是否已完成 STARTDT。</summary>
        public bool DataTransferActive { get; protected set; }

        public bool IsConnected
        {
            get { return Volatile.Read(ref _faulted) == 0 && _link.IsConnected; }
        }

        public int SentFrames
        {
            get { return Volatile.Read(ref _sentFrames); }
        }

        public int ReceivedFrames
        {
            get { return Volatile.Read(ref _receivedFrames); }
        }

        public int InvalidFrames { get; private set; }

        public int UnknownTypeFrames { get; private set; }

        public int DroppedFrames { get; private set; }

        /// <summary>会话取消令牌，供派生类的后台循环使用。</summary>
        protected CancellationToken SessionToken
        {
            get { return _cts.Token; }
        }

        public event EventHandler<Iec104TraceEventArgs> FrameTraced;

        /// <summary>启动读循环与定时器。</summary>
        protected void StartLoops()
        {
            if (_readTask != null) throw new InvalidOperationException("会话已经启动。");

            var now = Stopwatch.GetTimestamp();
            _lastReceiveTicks = now;
            _lastSendTicks = now;
            _lastAckProgressTicks = now;

            _readTask = Task.Run((Func<Task>)ReadLoopAsync);
            _timerTask = Task.Run((Func<Task>)TimerLoopAsync);
        }

        /// <summary>处理收到的 ASDU。</summary>
        protected abstract void OnAsdu(Asdu asdu);

        /// <summary>STARTDT 完成回调。</summary>
        protected virtual void OnStarted()
        {
        }

        /// <summary>链路故障回调。</summary>
        protected abstract void OnFault(Exception exception);

        private async Task ReadLoopAsync()
        {
            var buffer = new byte[8192];
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var read = await _link.ReadAsync(buffer, _cts.Token).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        Fault(new System.IO.IOException("对端关闭连接。"));
                        return;
                    }

                    _lastReceiveTicks = Stopwatch.GetTimestamp();
                    _parser.Append(buffer, 0, read);

                    byte[] frame;
                    while (_parser.TryReadFrame(out frame))
                    {
                        await HandleFrameAsync(frame).ConfigureAwait(false);
                        if (Volatile.Read(ref _faulted) != 0) return;
                    }
                }
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
        }

        private async Task HandleFrameAsync(byte[] raw)
        {
            ApciFrame frame;
            string error;
            if (!ApciFrame.TryParse(raw, 0, raw.Length, out frame, out error))
            {
                InvalidFrames++;
                Trace(false, raw, "无法解析：" + error);
                return;
            }

            switch (frame.FrameType)
            {
                case ApciFrameType.Information:
                    await HandleInformationAsync(frame, raw).ConfigureAwait(false);
                    break;

                case ApciFrameType.Supervisory:
                    Interlocked.Increment(ref _receivedFrames);
                    Trace(false, raw, $"S 帧 N(R)={frame.ReceiveSequence}");
                    Acknowledge(frame.ReceiveSequence);
                    break;

                default:
                    Interlocked.Increment(ref _receivedFrames);
                    Trace(false, raw, DescribeFunction(frame.Function));
                    await HandleUnnumberedAsync(frame.Function).ConfigureAwait(false);
                    break;
            }
        }

        private async Task HandleInformationAsync(ApciFrame frame, byte[] raw)
        {
            Interlocked.Increment(ref _receivedFrames);

            if (!DataTransferActive)
            {
                DroppedFrames++;
                return;
            }

            if (frame.SendSequence != _receiveSequence)
            {
                Fault(new Iec104ProtocolException(
                    Iec104ErrorKind.Protocol,
                    $"接收序号不连续：期望 {_receiveSequence}，实际 {frame.SendSequence}。"));
                return;
            }

            _receiveSequence = Next(_receiveSequence);
            if (Interlocked.CompareExchange(ref _receivedSinceAck, 1, 0) == 0)
            {
                _firstUnackedReceiveTicks = Stopwatch.GetTimestamp();
            }
            else
            {
                Interlocked.Increment(ref _receivedSinceAck);
            }

            Acknowledge(frame.ReceiveSequence);

            // w 阈值：累计收到 W 个 I 帧必须立即确认，不能只依赖 t2。
            if (Volatile.Read(ref _receivedSinceAck) >= W)
            {
                await SendSupervisoryAsync(_cts.Token).ConfigureAwait(false);
            }

            Asdu asdu;
            try
            {
                asdu = Asdu.Parse(frame.Asdu, 0, frame.Asdu.Length);
            }
            catch (Iec104ProtocolException ex) when (ex.Kind == Iec104ErrorKind.UnknownType)
            {
                UnknownTypeFrames++;
                Trace(false, raw, $"I 帧 N(S)={frame.SendSequence} 未知类型 {frame.Asdu[0]}");
                return;
            }
            catch (Exception ex)
            {
                Fault(ex);
                return;
            }

            Trace(false, raw, DescribeAsdu(frame, asdu));

            try
            {
                OnAsdu(asdu);
            }
            catch (Exception ex)
            {
                Fault(ex);
            }
        }

        private async Task HandleUnnumberedAsync(ApciFunction function)
        {
            switch (function)
            {
                case ApciFunction.TestFrameActivation:
                    await SendUnnumberedAsync(ApciFunction.TestFrameConfirmation, _cts.Token).ConfigureAwait(false);
                    break;

                case ApciFunction.TestFrameConfirmation:
                    Interlocked.Exchange(ref _testPending, 0);
                    break;

                case ApciFunction.StartDataTransferActivation:
                    DataTransferActive = true;
                    await SendUnnumberedAsync(ApciFunction.StartDataTransferConfirmation, _cts.Token).ConfigureAwait(false);
                    OnStarted();
                    break;

                case ApciFunction.StartDataTransferConfirmation:
                    DataTransferActive = true;
                    OnStarted();
                    break;

                case ApciFunction.StopDataTransferActivation:
                    DataTransferActive = false;
                    await SendUnnumberedAsync(ApciFunction.StopDataTransferConfirmation, _cts.Token).ConfigureAwait(false);
                    break;

                case ApciFunction.StopDataTransferConfirmation:
                    DataTransferActive = false;
                    break;
            }
        }

        private async Task TimerLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    await Task.Delay(TickMilliseconds, _cts.Token).ConfigureAwait(false);

                    if (!DataTransferActive) continue;

                    if (Volatile.Read(ref _receivedSinceAck) > 0 && Elapsed(_firstUnackedReceiveTicks) >= T2.TotalSeconds)
                    {
                        await SendSupervisoryAsync(_cts.Token).ConfigureAwait(false);
                    }

                    if (Volatile.Read(ref _unacked) > 0 && Elapsed(_lastAckProgressTicks) >= T1.TotalSeconds)
                    {
                        Fault(new TimeoutException("t1 超时：已发送的 I 帧未在时限内被确认。"));
                        return;
                    }

                    if (Volatile.Read(ref _testPending) != 0 && Elapsed(_testSentTicks) >= T1.TotalSeconds)
                    {
                        Fault(new TimeoutException("t1 超时：TESTFR 未收到确认。"));
                        return;
                    }

                    var lastActivity = Math.Max(_lastSendTicks, _lastReceiveTicks);
                    if (Volatile.Read(ref _testPending) == 0 && Elapsed(lastActivity) >= T3.TotalSeconds)
                    {
                        Interlocked.Exchange(ref _testPending, 1);
                        _testSentTicks = Stopwatch.GetTimestamp();
                        await SendUnnumberedAsync(ApciFunction.TestFrameActivation, _cts.Token).ConfigureAwait(false);
                    }
                }
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
        }

        /// <summary>发送 I 帧，遵守 k 窗口。</summary>
        protected async Task SendInformationAsync(byte[] asdu, CancellationToken cancellationToken)
        {
            await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (Volatile.Read(ref _unacked) >= K)
                {
                    if (Elapsed(_lastAckProgressTicks) >= T1.TotalSeconds)
                        throw new TimeoutException("发送窗口已满且长时间未收到确认。");

                    await Task.Delay(20, cancellationToken).ConfigureAwait(false);
                }

                await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                byte[] bytes;
                try
                {
                    var frame = ApciFrame.CreateInformation(_sendSequence, _receiveSequence, asdu);
                    bytes = frame.ToBytes();
                    _sendSequence = Next(_sendSequence);
                    if (Interlocked.Increment(ref _unacked) == 1)
                    {
                        _lastAckProgressTicks = Stopwatch.GetTimestamp();
                    }

                    await _link.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                    _lastSendTicks = Stopwatch.GetTimestamp();
                }
                finally
                {
                    _writeGate.Release();
                }

                Interlocked.Increment(ref _sentFrames);
                Trace(true, bytes, "I 帧 N(S)=" + ((_sendSequence - 1) & Iec104Constants.SequenceMask));
            }
            finally
            {
                _sendGate.Release();
            }
        }

        /// <summary>发送 S 帧确认。</summary>
        protected async Task SendSupervisoryAsync(CancellationToken cancellationToken)
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            byte[] bytes;
            try
            {
                Interlocked.Exchange(ref _receivedSinceAck, 0);
                bytes = ApciFrame.CreateSupervisory(_receiveSequence).ToBytes();
                await _link.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                _lastSendTicks = Stopwatch.GetTimestamp();
            }
            finally
            {
                _writeGate.Release();
            }

            Interlocked.Increment(ref _sentFrames);
            Trace(true, bytes, "S 帧 N(R)=" + _receiveSequence);
        }

        /// <summary>发送 U 帧。</summary>
        protected async Task SendUnnumberedAsync(ApciFunction function, CancellationToken cancellationToken)
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            byte[] bytes;
            try
            {
                bytes = ApciFrame.CreateUnnumbered(function).ToBytes();
                await _link.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                _lastSendTicks = Stopwatch.GetTimestamp();
            }
            finally
            {
                _writeGate.Release();
            }

            Interlocked.Increment(ref _sentFrames);
            Trace(true, bytes, DescribeFunction(function));
        }

        private void Acknowledge(ushort receiveNumber)
        {
            var delta = (receiveNumber - _acknowledgedSequence + 32768) % 32768;
            if (delta > Volatile.Read(ref _unacked))
            {
                Fault(new Iec104ProtocolException(
                    Iec104ErrorKind.Protocol,
                    $"确认序号越界：N(R)={receiveNumber}，已确认 {_acknowledgedSequence}，未确认 {_unacked}。"));
                return;
            }

            _acknowledgedSequence = receiveNumber;
            Interlocked.Add(ref _unacked, -delta);
            _lastAckProgressTicks = Stopwatch.GetTimestamp();
        }

        /// <summary>触发故障：取消、关闭链路、通知派生类。</summary>
        protected void Fault(Exception exception)
        {
            if (Interlocked.Exchange(ref _faulted, 1) != 0) return;

            try { _cts.Cancel(); } catch { }
            try { _link.Dispose(); } catch { }

            try
            {
                OnFault(exception);
            }
            catch
            {
            }
        }

        /// <summary>停止会话。</summary>
        public virtual async Task StopAsync()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0) return;

            try { _cts.Cancel(); } catch { }
            try { _link.Dispose(); } catch { }

            var tasks = new[] { _readTask, _timerTask };
            for (var i = 0; i < tasks.Length; i++)
            {
                var task = tasks[i];
                if (task == null) continue;
                try { await task.ConfigureAwait(false); } catch { }
            }

            _sendGate.Dispose();
            _writeGate.Dispose();
            _cts.Dispose();
        }

        public void Dispose()
        {
            try
            {
                StopAsync().GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        private static ushort Next(ushort sequence)
        {
            return (ushort)((sequence + 1) & Iec104Constants.SequenceMask);
        }

        private static double Elapsed(long startTicks)
        {
            return (Stopwatch.GetTimestamp() - startTicks) / (double)Stopwatch.Frequency;
        }

        private void Trace(bool sending, byte[] data, string meaning)
        {
            if (!EnableTrace) return;

            var handler = FrameTraced;
            if (handler == null) return;

            try
            {
                handler(this, new Iec104TraceEventArgs(sending, data, meaning));
            }
            catch
            {
            }
        }

        private static string DescribeFunction(ApciFunction function)
        {
            switch (function)
            {
                case ApciFunction.StartDataTransferActivation: return "STARTDT act";
                case ApciFunction.StartDataTransferConfirmation: return "STARTDT con";
                case ApciFunction.StopDataTransferActivation: return "STOPDT act";
                case ApciFunction.StopDataTransferConfirmation: return "STOPDT con";
                case ApciFunction.TestFrameActivation: return "TESTFR act";
                case ApciFunction.TestFrameConfirmation: return "TESTFR con";
                default: return function.ToString();
            }
        }

        private static string DescribeAsdu(ApciFrame frame, Asdu asdu)
        {
            return $"I 帧 N(S)={frame.SendSequence} {asdu.TypeId} COT={asdu.Cause} 对象={asdu.Objects.Count}";
        }
    }
}
