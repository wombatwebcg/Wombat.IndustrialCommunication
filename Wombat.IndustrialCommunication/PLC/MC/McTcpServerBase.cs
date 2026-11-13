using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Abstractions;
using Wombat.IndustrialCommunication.Events;
using Wombat.IndustrialCommunication.Models;
using Wombat.IndustrialCommunication.Server;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC TCP 服务端基类：按首字节嗅探 1E / 3E / 4E，缓冲拆分请求帧，
    /// 解析与响应全部走 <see cref="McFrameCodec"/>，与客户端共用同一份编解码。
    /// </summary>
    public class McTcpServerBase : DeviceServerBase
    {
        /// <summary>单帧请求上限，超过视为脏流</summary>
        private const int MaxFrameLength = 8192;

        private readonly Dictionary<INetworkSession, List<byte>> _sessionBuffers =
            new Dictionary<INetworkSession, List<byte>>();

        private readonly object _bufferSyncRoot = new object();

        private readonly IServerListener _listener;

        /// <summary>服务器消息传输</summary>
        protected readonly ServerMessageTransport _transport;

        /// <summary>构造服务端基类</summary>
        /// <param name="transport">服务器消息传输</param>
        public McTcpServerBase(ServerMessageTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));

            // MC 字数据为「低字在前、字内小端」，与客户端保持同一口径
            DataFormat = EndianFormat.DCBA;
            IsReverse = false;

            _transport.RegisterMessageHandler(HandleMessage);

            _listener = _transport.StreamResource as IServerListener;
            if (_listener != null)
            {
                _listener.ClientDisconnected += OnClientDisconnected;
            }
        }

        /// <summary>数据区</summary>
        public McDataStore DataStore { get; } = new McDataStore();

        /// <summary>
        /// 地址解析使用的 PLC 系列，只影响 IReadWrite 接口里 X/Y 的编号进制与 1E 别名展开。
        /// 报文里的系列由子指令决定，不受此设置影响。
        /// </summary>
        public McSeries Series { get; set; } = McSeries.Q;

        /// <summary>是否正在监听</summary>
        public virtual bool IsListening
        {
            get { return _transport.StreamResource != null && _transport.StreamResource.Connected; }
        }

        /// <inheritdoc />
        public override string Version
        {
            get { return "MC 1.0"; }
        }

        /// <summary>启动服务端</summary>
        public virtual Task<OperationResult> StartAsync()
        {
            return _transport.StartAsync();
        }

        /// <summary>停止服务端</summary>
        public virtual Task<OperationResult> StopAsync()
        {
            return _transport.StopAsync();
        }

        /// <summary>数据区被写入后触发，供子类做快照标记</summary>
        protected virtual void OnDataWritten()
        {
        }

        /// <summary>
        /// 处理一条已拆分的请求帧。返回 null 表示帧无法解析，调用方应断开该会话。
        /// </summary>
        protected virtual byte[] ProcessRequest(McFrameType frameType, byte[] frame)
        {
            McPacket request;
            try
            {
                request = McFrameCodec.ParseRequest(frameType, frame);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning("MC 请求解析失败（{FrameType}）：{Message}", frameType, ex.Message);
                return null;
            }

            if (request.Command != McFrameCodec.CommandBatchRead &&
                request.Command != McFrameCodec.CommandBatchWrite)
            {
                return McFrameCodec.BuildErrorResponse(request, McCommonMethods.EndCodeCommandNotSupported);
            }

            if (request.Device == null)
            {
                Logger?.LogDebug("MC 请求使用了无法识别的软元件码，已回 C05B");
                return McFrameCodec.BuildErrorResponse(request, McCommonMethods.EndCodeDeviceSpecificationError);
            }

            if (request.Device.IsBit != request.IsBit)
            {
                return McFrameCodec.BuildErrorResponse(request, McCommonMethods.EndCodeDeviceSpecificationError);
            }

            int maxPoints = McFrameCodec.GetMaxPoints(frameType, request.IsIqR, request.IsBit);
            if (request.Points <= 0 || request.Points > maxPoints)
            {
                return McFrameCodec.BuildErrorResponse(request, McCommonMethods.EndCodePointCountOutOfRange);
            }

            McDeviceKind kind = request.Device.Kind;
            if (!DataStore.CanAccess(kind, request.StartAddress, request.Points))
            {
                return McFrameCodec.BuildErrorResponse(request, McCommonMethods.EndCodeRequestBeyondMaxAddress);
            }

            if (request.IsWrite)
            {
                int expected = request.IsBit ? (request.Points + 1) / 2 : request.Points * 2;
                if (request.Data == null || request.Data.Length != expected)
                {
                    return McFrameCodec.BuildErrorResponse(request, McCommonMethods.EndCodeRequestDataLengthError);
                }

                if (request.IsBit)
                {
                    DataStore.WriteBits(kind, request.StartAddress,
                        McCommonMethods.UnpackBitData(request.Data, request.Points));
                }
                else
                {
                    DataStore.WriteWords(kind, request.StartAddress, request.Data);
                }

                OnDataWritten();
                return McFrameCodec.BuildResponse(request, McCommonMethods.EndCodeNormal, null);
            }

            byte[] data = request.IsBit
                ? McCommonMethods.PackBitData(DataStore.ReadBits(kind, request.StartAddress, request.Points))
                : DataStore.ReadWords(kind, request.StartAddress, request.Points);

            return McFrameCodec.BuildResponse(request, McCommonMethods.EndCodeNormal, data);
        }

        private void HandleMessage(ReceivedMessage message)
        {
            if (message == null || message.Data == null || message.Data.Length == 0)
                return;

            List<byte> buffer;
            lock (_bufferSyncRoot)
            {
                if (!_sessionBuffers.TryGetValue(message.Session, out buffer))
                {
                    buffer = new List<byte>();
                    _sessionBuffers[message.Session] = buffer;
                }
            }

            buffer.AddRange(message.Data);

            while (true)
            {
                McFrameType frameType;
                byte[] frame;
                try
                {
                    if (!TryDequeueFrame(buffer, out frameType, out frame))
                        return;
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning("MC 请求帧非法，断开会话：{Message}", ex.Message);
                    CloseSession(message.Session, buffer);
                    return;
                }

                byte[] response = ProcessRequest(frameType, frame);
                if (response == null)
                {
                    CloseSession(message.Session, buffer);
                    return;
                }

                var sendResult = _transport.SendToSessionAsync(message.Session, response)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                if (!sendResult.IsSuccess)
                {
                    Logger?.LogError("发送 MC 响应失败：{Message}", sendResult.Message);
                    CloseSession(message.Session, buffer);
                    return;
                }
            }
        }

        private static bool TryDequeueFrame(List<byte> buffer, out McFrameType frameType, out byte[] frame)
        {
            frameType = McFrameType.Q3E;
            frame = null;

            if (buffer.Count == 0)
                return false;

            frameType = McFrameCodec.SniffFrameType(buffer[0]);
            if (buffer.Count < McFrameCodec.GetRequestHeaderLength(frameType))
                return false;

            int totalLength;
            if (!McFrameCodec.TryGetRequestLength(frameType, buffer.ToArray(), out totalLength))
                return false;

            if (totalLength <= 0 || totalLength > MaxFrameLength)
                throw new InvalidOperationException($"MC 请求帧长度非法：{totalLength} 字节");

            if (buffer.Count < totalLength)
                return false;

            frame = buffer.GetRange(0, totalLength).ToArray();
            buffer.RemoveRange(0, totalLength);
            return true;
        }

        private void CloseSession(INetworkSession session, List<byte> buffer)
        {
            buffer.Clear();
            try
            {
                session?.Close();
            }
            catch (Exception ex)
            {
                Logger?.LogDebug("关闭 MC 会话失败：{Message}", ex.Message);
            }
        }

        private void OnClientDisconnected(object sender, SessionEventArgs e)
        {
            lock (_bufferSyncRoot)
            {
                _sessionBuffers.Remove(e.Session);
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_listener != null)
                {
                    _listener.ClientDisconnected -= OnClientDisconnected;
                }

                try
                {
                    if (IsListening)
                        _transport.StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Logger?.LogDebug("停止 MC 服务端失败：{Message}", ex.Message);
                }

                _transport?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
