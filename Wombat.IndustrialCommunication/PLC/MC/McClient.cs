using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Abstractions;
using Wombat.IndustrialCommunication.Adapters;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 三菱 MC 协议客户端（TCP）。支持 A-1E / Qna-3E / Qna-4E 三种二进制帧。
    /// </summary>
    public class McClient : McCommunication, IDeviceClient
    {
        private readonly TcpClientAdapter _tcpClientAdapter;
        private readonly McEthernetTransport _transport;
        private ILogger _logger;

        /// <summary>构造客户端</summary>
        /// <param name="ipAddress">PLC IP 地址</param>
        /// <param name="port">端口号，默认 5000</param>
        /// <param name="frameType">帧类型</param>
        /// <param name="series">PLC 系列，决定子指令、软元件码宽度与 X/Y 进制</param>
        /// <param name="timeout">连接/发送/接收超时</param>
        public McClient(string ipAddress, int port = 5000, McFrameType frameType = McFrameType.Q3E,
            McSeries series = McSeries.Q, TimeSpan? timeout = null)
            : base(new McEthernetTransport(new TcpClientAdapter(ipAddress, port), frameType),
                new McProfile(frameType, series))
        {
            _transport = (McEthernetTransport)Transport;
            _tcpClientAdapter = (TcpClientAdapter)Transport.StreamResource;

            if (timeout.HasValue)
            {
                _tcpClientAdapter.ConnectTimeout = timeout.Value;
                _tcpClientAdapter.ReceiveTimeout = timeout.Value;
                _tcpClientAdapter.SendTimeout = timeout.Value;
            }
        }

        /// <summary>IP 地址</summary>
        public string IpAddress
        {
            get { return _tcpClientAdapter.IpAddress; }
        }

        /// <summary>端口号</summary>
        public int Port
        {
            get { return _tcpClientAdapter.Port; }
        }

        /// <summary>帧类型</summary>
        public McFrameType FrameType
        {
            get { return _transport.FrameType; }
        }

        /// <summary>PLC 系列</summary>
        public McSeries Series
        {
            get { return Profile.Series; }
        }

        /// <summary>X/Y 编号进制的手工覆盖（现场编号规则与默认不一致时使用）</summary>
        public int? AddressRadixOverride
        {
            get { return Profile.AddressRadixOverride; }
            set { Profile.AddressRadixOverride = value; }
        }

        /// <summary>超时时间（同时设置连接、发送、接收）</summary>
        public TimeSpan Timeout
        {
            get { return _tcpClientAdapter.ConnectTimeout; }
            set
            {
                _tcpClientAdapter.ConnectTimeout = value;
                _tcpClientAdapter.ReceiveTimeout = value;
                _tcpClientAdapter.SendTimeout = value;
            }
        }

        /// <summary>是否已连接</summary>
        public bool IsConnected
        {
            get { return _tcpClientAdapter != null && _tcpClientAdapter.Connected; }
        }

        /// <inheritdoc />
        public bool Connected
        {
            get { return IsConnected; }
        }

        /// <summary>日志</summary>
        public new ILogger Logger
        {
            get { return _logger; }
            set
            {
                _logger = value;
                base.Logger = value;
                if (_transport != null)
                    _transport.Logger = value;
                if (_tcpClientAdapter != null)
                    _tcpClientAdapter.Logger = value;
            }
        }

        /// <summary>是否输出调试日志</summary>
        public new bool EnableDebugLog
        {
            get { return base.EnableDebugLog; }
            set
            {
                base.EnableDebugLog = value;
                if (_transport != null)
                    _transport.EnableDebugLog = value;
                if (_tcpClientAdapter != null)
                    _tcpClientAdapter.EnableDebugLog = value;
            }
        }

        /// <inheritdoc />
        public int Retries
        {
            get { return _transport.Retries; }
            set { _transport.Retries = value; }
        }

        /// <inheritdoc />
        public TimeSpan WaitToRetryMilliseconds
        {
            get { return _transport.WaitToRetryMilliseconds; }
            set { _transport.WaitToRetryMilliseconds = value; }
        }

        /// <inheritdoc />
        public TimeSpan ConnectTimeout
        {
            get { return _tcpClientAdapter.ConnectTimeout; }
            set { _tcpClientAdapter.ConnectTimeout = value; }
        }

        /// <inheritdoc />
        public TimeSpan ReceiveTimeout
        {
            get { return _tcpClientAdapter.ReceiveTimeout; }
            set { _tcpClientAdapter.ReceiveTimeout = value; }
        }

        /// <inheritdoc />
        public TimeSpan SendTimeout
        {
            get { return _tcpClientAdapter.SendTimeout; }
            set { _tcpClientAdapter.SendTimeout = value; }
        }

        /// <inheritdoc />
        public TimeSpan ResponseInterval
        {
            get { return _transport.ResponseInterval; }
            set { _transport.ResponseInterval = value; }
        }

        /// <inheritdoc />
        public async Task<OperationResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            // MC 没有独立的协议握手，建立 TCP 连接即可收发
            return await _tcpClientAdapter.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<OperationResult> DisconnectAsync()
        {
            return await _tcpClientAdapter.DisconnectAsync().ConfigureAwait(false);
        }

        /// <inheritdoc />
        protected internal override ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length,
            DataTypeEnums dataType, bool isBit = false)
        {
            return ReadAsync(address, length, dataType, isBit, CancellationToken.None);
        }

        /// <inheritdoc />
        protected internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length,
            DataTypeEnums dataType, bool isBit, CancellationToken cancellationToken)
        {
            var result = await base.ReadAsync(address, length, dataType, isBit, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess && result.FailureKind == OperationFailureKind.ProtocolViolation)
                await DisconnectQuietlyAsync().ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc />
        protected internal override Task<OperationResult> WriteAsync(string address, byte[] data,
            DataTypeEnums dataType, bool isBit = false)
        {
            return WriteAsync(address, data, dataType, isBit, CancellationToken.None);
        }

        /// <inheritdoc />
        protected internal override async Task<OperationResult> WriteAsync(string address, byte[] data,
            DataTypeEnums dataType, bool isBit, CancellationToken cancellationToken)
        {
            var result = await base.WriteAsync(address, data, dataType, isBit, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess && result.FailureKind == OperationFailureKind.ProtocolViolation)
                await DisconnectQuietlyAsync().ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// 帧错位（4E 序列号不回显、帧结构非法）意味着流里残留了脏数据，
        /// 直接断开让上层重连，避免后续每一帧都错位。
        /// </summary>
        private async Task DisconnectQuietlyAsync()
        {
            try
            {
                await _tcpClientAdapter.DisconnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "MC 客户端在帧错位后断开连接失败");
            }
        }
    }
}
