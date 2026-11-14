using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Iec104;
using Wombat.IndustrialCommunication.Iec104.Options;
using Wombat.IndustrialCommunication.Iec104.Protocol;
using Wombat.IndustrialCommunication.Iec104.Transport;

namespace Wombat.IndustrialCommunication.Iec104.Tests
{
    /// <summary>从站侧通道：读取主站发来的帧，发送帧给主站。</summary>
    internal interface ISlaveChannel
    {
        Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken);

        void Send(byte[] frame);
    }

    /// <summary>主站到从站、从站到主站的双向队列链路。</summary>
    internal sealed class QueueLink : IIec104Link
    {
        private readonly BlockingCollection<byte[]> _toMaster = new BlockingCollection<byte[]>();
        private readonly BlockingCollection<byte[]> _fromMaster = new BlockingCollection<byte[]>();
        private int _closed;

        public bool IsConnected
        {
            get { return Volatile.Read(ref _closed) == 0; }
        }

        public EndPoint RemoteEndPoint
        {
            get { return new IPEndPoint(IPAddress.Loopback, 2404); }
        }

        public void EnqueueToMaster(byte[] data)
        {
            if (Volatile.Read(ref _closed) == 0) _toMaster.Add(data);
        }

        public Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            return Task.Run(new Func<int>(() =>
            {
                try
                {
                    var chunk = _toMaster.Take(cancellationToken);
                    var count = Math.Min(buffer.Length, chunk.Length);
                    Buffer.BlockCopy(chunk, 0, buffer, 0, count);
                    return count;
                }
                catch (InvalidOperationException)
                {
                    return 0;
                }
            }), cancellationToken);
        }

        public Task<int> ReadFromMasterAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            return Task.Run(new Func<int>(() =>
            {
                try
                {
                    var chunk = _fromMaster.Take(cancellationToken);
                    var count = Math.Min(buffer.Length, chunk.Length);
                    Buffer.BlockCopy(chunk, 0, buffer, 0, count);
                    return count;
                }
                catch (InvalidOperationException)
                {
                    return 0;
                }
            }), cancellationToken);
        }

        public Task WriteAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            var copy = new byte[buffer.Length];
            Buffer.BlockCopy(buffer, 0, copy, 0, buffer.Length);
            _fromMaster.Add(copy, cancellationToken);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            try { _toMaster.CompleteAdding(); } catch { }
            try { _fromMaster.CompleteAdding(); } catch { }
        }
    }

    /// <summary>队列链路之上的从站通道。</summary>
    internal sealed class QueueSlaveChannel : ISlaveChannel
    {
        private readonly QueueLink _link;

        public QueueSlaveChannel(QueueLink link)
        {
            _link = link;
        }

        public Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            return _link.ReadFromMasterAsync(buffer, cancellationToken);
        }

        public void Send(byte[] frame)
        {
            _link.EnqueueToMaster(frame);
        }
    }

    /// <summary>最小从站：只实现 STARTDT、总召、遥控应答，用于验证主站。</summary>
    internal sealed class FakeSlave : IDisposable
    {
        private readonly ISlaveChannel _channel;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Iec104FrameParser _parser = new Iec104FrameParser();
        private readonly ushort _commonAddress;
        private Task _task;
        private ushort _sendSequence;
        private ushort _receiveSequence;

        public FakeSlave(ISlaveChannel channel, ushort commonAddress)
        {
            _channel = channel;
            _commonAddress = commonAddress;
        }

        public bool RejectCommands { get; set; }

        public List<Asdu> Received { get; } = new List<Asdu>();

        public void Start()
        {
            _task = Task.Run((Func<Task>)RunAsync);
        }

        private async Task RunAsync()
        {
            var buffer = new byte[8192];
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var read = await _channel.ReadAsync(buffer, _cts.Token).ConfigureAwait(false);
                    if (read <= 0) return;

                    _parser.Append(buffer, 0, read);

                    byte[] raw;
                    while (_parser.TryReadFrame(out raw))
                    {
                        Handle(raw);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void Handle(byte[] raw)
        {
            ApciFrame frame;
            string error;
            if (!ApciFrame.TryParse(raw, 0, raw.Length, out frame, out error)) return;

            if (frame.FrameType == ApciFrameType.Unnumbered)
            {
                if (frame.Function == ApciFunction.StartDataTransferActivation)
                {
                    SendUnnumbered(ApciFunction.StartDataTransferConfirmation);
                }

                return;
            }

            if (frame.FrameType == ApciFrameType.Supervisory)
            {
                return;
            }

            _receiveSequence = (ushort)((frame.SendSequence + 1) & Iec104Constants.SequenceMask);
            var asdu = Asdu.Parse(frame.Asdu, 0, frame.Asdu.Length);
            Received.Add(asdu);

            switch (asdu.TypeId)
            {
                case Iec104TypeId.InterrogationCommand:
                    Send(AsduBuilder.InterrogationConfirmation(_commonAddress));
                    Send(AsduBuilder.Build(
                        Iec104TypeId.MeasuredFloat,
                        _commonAddress,
                        Iec104Cause.InterrogatedByStation,
                        new[]
                        {
                            Iec104InformationObject.MeasuredFloat(1, 50.5f),
                            Iec104InformationObject.MeasuredFloat(2, 220.25f),
                        }));
                    Send(AsduBuilder.Build(
                        Iec104TypeId.SinglePointWithTime,
                        _commonAddress,
                        Iec104Cause.InterrogatedByStation,
                        new[]
                        {
                            Iec104InformationObject.SinglePointWithTime(
                                101,
                                true,
                                new DateTime(2026, 11, 13, 10, 20, 30, 500)),
                        }));
                    Send(AsduBuilder.InterrogationTermination(_commonAddress));
                    break;

                case Iec104TypeId.SingleCommand:
                case Iec104TypeId.DoubleCommand:
                    var item = asdu.Objects[0];
                    var isSelect = (item.Element[0] & 0x80) != 0;
                    Send(AsduBuilder.Build(
                        asdu.TypeId,
                        _commonAddress,
                        Iec104Cause.ActivationConfirmation,
                        new[] { item },
                        negative: RejectCommands));

                    if (!RejectCommands && !isSelect)
                    {
                        Send(AsduBuilder.Build(
                            asdu.TypeId,
                            _commonAddress,
                            Iec104Cause.ActivationTermination,
                            new[] { item }));
                    }

                    break;
            }
        }

        private void Send(byte[] asdu)
        {
            var frame = ApciFrame.CreateInformation(_sendSequence, _receiveSequence, asdu).ToBytes();
            _sendSequence = (ushort)((_sendSequence + 1) & Iec104Constants.SequenceMask);
            _channel.Send(frame);
        }

        private void SendUnnumbered(ApciFunction function)
        {
            _channel.Send(ApciFrame.CreateUnnumbered(function).ToBytes());
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _task?.Wait(2000); } catch { }
            _cts.Dispose();
        }
    }

    /// <summary>主站测试夹具：内存链路加最小从站。</summary>
    internal sealed class MasterHarness : IAsyncDisposable
    {
        private readonly FakeSlave _slave;

        public MasterHarness(bool rejectCommands = false, Model.Iec104PointTable pointTable = null)
        {
            var link = new QueueLink();
            Link = link;
            _slave = new FakeSlave(new QueueSlaveChannel(link), 1) { RejectCommands = rejectCommands };
            _slave.Start();

            var options = new Iec104MasterOptions
            {
                CommonAddress = 1,
                T1 = TimeSpan.FromSeconds(2),
                T2 = TimeSpan.FromSeconds(1),
                T3 = TimeSpan.FromSeconds(10),
                InterrogationTimeout = TimeSpan.FromSeconds(2),
                CommandTimeout = TimeSpan.FromSeconds(2),
                PointTable = pointTable,
            };

            Master = new Iec104Master(options, _ => Task.FromResult<IIec104Link>(link));
        }

        public Iec104Master Master { get; }

        public QueueLink Link { get; }

        public FakeSlave Slave
        {
            get { return _slave; }
        }

        public async ValueTask DisposeAsync()
        {
            await Master.DisposeAsync();
            _slave.Dispose();
        }
    }
}
