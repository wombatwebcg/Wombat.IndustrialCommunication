using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Adapters;
using Wombat.IndustrialCommunication.Models;
using Wombat.IndustrialCommunication.Server;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC TCP 服务端：模拟三菱 PLC，自动识别 1E / 3E / 4E 请求，提供数据区直接读写与快照持久化。
    /// </summary>
    public class McTcpServer : McTcpServerBase, IDeviceServer
    {
        private const int DefaultTimeoutMs = 3000;

        private readonly TcpServerAdapter _tcpServerAdapter;

        /// <summary>构造服务端（0.0.0.0:5000）</summary>
        public McTcpServer()
            : this("0.0.0.0", 5000)
        {
        }

        /// <summary>构造服务端</summary>
        public McTcpServer(IPEndPoint ipEndPoint)
            : base(CreateTransport(ipEndPoint))
        {
            _tcpServerAdapter = (TcpServerAdapter)base._transport.StreamResource;
            IPEndPoint = ipEndPoint;
            ConfigureSnapshotPersistence();
        }

        /// <summary>构造服务端</summary>
        public McTcpServer(string ip, int port)
            : this(CreateEndPoint(ip, port))
        {
        }

        /// <summary>构造服务端，并指定地址解析使用的 PLC 系列</summary>
        public McTcpServer(string ip, int port, McSeries series)
            : this(ip, port)
        {
            Series = series;
        }

        /// <summary>IP 终结点</summary>
        public IPEndPoint IPEndPoint { get; private set; }

        /// <inheritdoc />
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromMilliseconds(DefaultTimeoutMs);

        /// <inheritdoc />
        public TimeSpan ReceiveTimeout
        {
            get { return _tcpServerAdapter.ReceiveTimeout; }
            set { _tcpServerAdapter.ReceiveTimeout = value; }
        }

        /// <inheritdoc />
        public TimeSpan SendTimeout
        {
            get { return _tcpServerAdapter.SendTimeout; }
            set { _tcpServerAdapter.SendTimeout = value; }
        }

        /// <summary>最大连接数</summary>
        public int MaxConnections
        {
            get { return _tcpServerAdapter.MaxConnections; }
            set { _tcpServerAdapter.MaxConnections = value; }
        }

        /// <summary>空闲会话超时</summary>
        public TimeSpan IdleSessionTimeout
        {
            get { return _tcpServerAdapter.IdleSessionTimeout; }
            set { _tcpServerAdapter.IdleSessionTimeout = value; }
        }

        /// <summary>是否启用快照持久化</summary>
        public bool EnableSnapshotPersistence { get; set; }

        /// <summary>快照文件路径</summary>
        public string SnapshotFilePath { get; set; }

        /// <inheritdoc />
        public void UseLogger(ILogger logger)
        {
            Logger = logger;
            _tcpServerAdapter.UseLogger(logger);
        }

        /// <inheritdoc />
        public async Task<OperationResult> ListenAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryLoadSnapshot();
            return await StartAsync().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<OperationResult> ShutdownAsync()
        {
            TrySaveSnapshot();
            return await StopAsync().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void ConfigureSnapshotPersistence(string name = null)
        {
            SnapshotFilePath = SnapshotFilePathHelper.Build("McTcpServer", IPEndPoint.Port.ToString(), name);
        }

        /// <summary>立即保存快照</summary>
        public OperationResult SaveSnapshot()
        {
            if (!EnableSnapshotPersistence || string.IsNullOrWhiteSpace(SnapshotFilePath))
                return OperationResult.CreateSuccessResult();

            try
            {
                ServerSnapshotPersistence.SaveMcSnapshot(SnapshotFilePath, DataStore);
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "保存 MC 快照失败: {SnapshotFilePath}", SnapshotFilePath);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }

        /// <inheritdoc />
        public OperationResult DeleteSnapshot()
        {
            try
            {
                ServerSnapshotPersistence.DeleteSnapshot(SnapshotFilePath);
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "删除 MC 快照失败: {SnapshotFilePath}", SnapshotFilePath);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }

        /// <inheritdoc />
        public OperationResult ResetDataAndDeleteSnapshot()
        {
            DataStore.Reset();
            return DeleteSnapshot();
        }

        private void TryLoadSnapshot()
        {
            if (!EnableSnapshotPersistence || string.IsNullOrWhiteSpace(SnapshotFilePath) || !File.Exists(SnapshotFilePath))
                return;

            try
            {
                ServerSnapshotPersistence.LoadMcSnapshot(SnapshotFilePath, DataStore);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "加载 MC 快照失败: {SnapshotFilePath}", SnapshotFilePath);
            }
        }

        private void TrySaveSnapshot()
        {
            SaveSnapshot();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TrySaveSnapshot();
            }

            base.Dispose(disposing);
        }

        private static ServerMessageTransport CreateTransport(IPEndPoint ipEndPoint)
        {
            return new ServerMessageTransport(new TcpServerAdapter(ipEndPoint));
        }

        private static IPEndPoint CreateEndPoint(string ip, int port)
        {
            IPAddress address;
            if (!IPAddress.TryParse(ip, out address))
            {
                if (string.Equals(ip, "0.0.0.0", StringComparison.Ordinal) ||
                    string.Equals(ip, "any", StringComparison.OrdinalIgnoreCase))
                {
                    address = IPAddress.Any;
                }
                else
                {
                    address = Dns.GetHostEntry(ip).AddressList.FirstOrDefault() ?? IPAddress.Loopback;
                }
            }

            return new IPEndPoint(address, port);
        }

        #region 数据区操作接口

        /// <summary>
        /// 直接读数据区。位软元件返回每点 1 字节（0/1），字软元件返回每点 2 字节小端。
        /// </summary>
        /// <param name="kind">软元件种类</param>
        /// <param name="startPoint">起始编号（长定时器为元素编号）</param>
        /// <param name="count">位区为点数；字区为点数（长定时器为元素个数）</param>
        public OperationResult<byte[]> ReadArea(McDeviceKind kind, int startPoint, int count)
        {
            if (count <= 0)
                return OperationResult.CreateFailedResult<byte[]>("读取长度必须大于 0");

            McDeviceDescriptor descriptor;
            try
            {
                descriptor = McDeviceDescriptor.Find(kind);
            }
            catch (ArgumentException ex)
            {
                return OperationResult.CreateFailedResult<byte[]>(ex.Message);
            }

            int protocolPoints = descriptor.IsBit ? count : count * descriptor.WordsPerPoint;
            if (!DataStore.CanAccess(kind, startPoint, protocolPoints))
                return OperationResult.CreateFailedResult<byte[]>($"地址超出数据区范围: {kind}{startPoint}");

            if (descriptor.IsBit)
            {
                bool[] values = DataStore.ReadBits(kind, startPoint, count);
                var bytes = new byte[count];
                for (int i = 0; i < count; i++)
                    bytes[i] = values[i] ? (byte)1 : (byte)0;
                return OperationResult.CreateSuccessResult(bytes);
            }

            byte[] words = DataStore.ReadWords(kind, startPoint, protocolPoints);
            if (descriptor.WordsPerPoint == 1)
                return OperationResult.CreateSuccessResult(words);

            // 长定时器：每个元素的当前值（第 1 字）压紧成 2 字节/元素
            var compact = new byte[count * 2];
            for (int i = 0; i < count; i++)
                Array.Copy(words, i * descriptor.WordsPerPoint * 2, compact, i * 2, 2);
            return OperationResult.CreateSuccessResult(compact);
        }

        /// <summary>直接写数据区，字节布局与 <see cref="ReadArea"/> 一致</summary>
        public OperationResult WriteArea(McDeviceKind kind, int startPoint, byte[] data)
        {
            if (data == null || data.Length == 0)
                return OperationResult.CreateFailedResult("写入数据不能为空");

            McDeviceDescriptor descriptor;
            try
            {
                descriptor = McDeviceDescriptor.Find(kind);
            }
            catch (ArgumentException ex)
            {
                return OperationResult.CreateFailedResult(ex.Message);
            }

            int elements = descriptor.IsBit ? data.Length : (data.Length + 1) / 2;
            int protocolPoints = descriptor.IsBit ? data.Length : elements * descriptor.WordsPerPoint;
            if (!DataStore.CanAccess(kind, startPoint, protocolPoints))
                return OperationResult.CreateFailedResult($"地址超出数据区范围: {kind}{startPoint}");

            if (descriptor.IsBit)
            {
                var values = new bool[data.Length];
                for (int i = 0; i < data.Length; i++)
                    values[i] = data[i] != 0;
                DataStore.WriteBits(kind, startPoint, values);
                return OperationResult.CreateSuccessResult();
            }

            if (descriptor.WordsPerPoint == 1)
            {
                var words = new byte[elements * 2];
                Array.Copy(data, words, data.Length);
                DataStore.WriteWords(kind, startPoint, words);
                return OperationResult.CreateSuccessResult();
            }

            // 长定时器：只填每个元素的当前值，系统占用的字保持原值
            byte[] existing = DataStore.ReadWords(kind, startPoint, protocolPoints);
            for (int i = 0; i < elements; i++)
            {
                int source = i * 2;
                int target = i * descriptor.WordsPerPoint * 2;
                if (source + 1 < data.Length)
                {
                    existing[target] = data[source];
                    existing[target + 1] = data[source + 1];
                }
                else if (source < data.Length)
                {
                    existing[target] = data[source];
                }
            }

            DataStore.WriteWords(kind, startPoint, existing);
            return OperationResult.CreateSuccessResult();
        }

        #endregion

        #region IReadWrite 核心

        private McProfile GetAddressProfile()
        {
            return Series == McSeries.A
                ? new McProfile(McFrameType.A1E, McSeries.A)
                : new McProfile(McFrameType.Q3E, Series);
        }

        private OperationResult<McAddress> ParseAddress(string address, out bool isBitDevice)
        {
            isBitDevice = false;
            McAddress parsed;
            try
            {
                parsed = McAddress.Parse(address, GetAddressProfile());
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is OverflowException)
            {
                return OperationResult.CreateFailedResult<McAddress>(ex.Message);
            }

            isBitDevice = parsed.IsBitDevice;
            return OperationResult.CreateSuccessResult(parsed);
        }

        private OperationResult<byte[]> ReadWordsCore(string address, int byteLength)
        {
            if (byteLength <= 0)
                return OperationResult.CreateFailedResult<byte[]>("读取长度必须大于 0");

            bool isBitDevice;
            var addressResult = ParseAddress(address, out isBitDevice);
            if (!addressResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(addressResult.Message);
            if (isBitDevice)
                return OperationResult.CreateFailedResult<byte[]>("位软元件不支持字访问");

            McAddress parsed = addressResult.ResultValue;
            int elements = (byteLength + 1) / 2;
            int points = elements * parsed.WordsPerPoint;
            if (!DataStore.CanAccess(parsed.DeviceKind, parsed.Address, points))
                return OperationResult.CreateFailedResult<byte[]>($"地址超出数据区范围: {address}");

            byte[] words = DataStore.ReadWords(parsed.DeviceKind, parsed.Address, points);
            if (parsed.WordsPerPoint > 1)
            {
                var compact = new byte[elements * 2];
                for (int i = 0; i < elements; i++)
                    Array.Copy(words, i * parsed.WordsPerPoint * 2, compact, i * 2, 2);
                words = compact;
            }

            if (words.Length > byteLength)
            {
                var trimmed = new byte[byteLength];
                Array.Copy(words, trimmed, byteLength);
                words = trimmed;
            }

            return OperationResult.CreateSuccessResult(words);
        }

        private OperationResult WriteWordsCore(string address, byte[] data)
        {
            if (data == null || data.Length == 0)
                return OperationResult.CreateFailedResult("写入数据不能为空");

            bool isBitDevice;
            var addressResult = ParseAddress(address, out isBitDevice);
            if (!addressResult.IsSuccess)
                return OperationResult.CreateFailedResult(addressResult.Message);
            if (isBitDevice)
                return OperationResult.CreateFailedResult("位软元件不支持字写入");

            McAddress parsed = addressResult.ResultValue;
            int elements = (data.Length + 1) / 2;
            int points = elements * parsed.WordsPerPoint;
            if (!DataStore.CanAccess(parsed.DeviceKind, parsed.Address, points))
                return OperationResult.CreateFailedResult($"地址超出数据区范围: {address}");

            if (parsed.WordsPerPoint == 1)
            {
                var words = new byte[elements * 2];
                Array.Copy(data, words, data.Length);
                DataStore.WriteWords(parsed.DeviceKind, parsed.Address, words);
            }
            else
            {
                var words = new byte[points * 2];
                for (int i = 0; i < elements; i++)
                {
                    int source = i * 2;
                    int target = i * parsed.WordsPerPoint * 2;
                    if (source + 1 < data.Length)
                    {
                        words[target] = data[source];
                        words[target + 1] = data[source + 1];
                    }
                    else if (source < data.Length)
                    {
                        words[target] = data[source];
                    }
                }

                DataStore.WriteWords(parsed.DeviceKind, parsed.Address, words);
            }

            OnDataWritten();
            return OperationResult.CreateSuccessResult();
        }

        private OperationResult<bool[]> ReadBitsCore(string address, int points)
        {
            if (points <= 0)
                return OperationResult.CreateFailedResult<bool[]>("读取长度必须大于 0");

            bool isBitDevice;
            var addressResult = ParseAddress(address, out isBitDevice);
            if (!addressResult.IsSuccess)
                return OperationResult.CreateFailedResult<bool[]>(addressResult.Message);
            if (!isBitDevice)
                return OperationResult.CreateFailedResult<bool[]>("字软元件不支持位访问");

            McAddress parsed = addressResult.ResultValue;
            if (!DataStore.CanAccess(parsed.DeviceKind, parsed.Address, points))
                return OperationResult.CreateFailedResult<bool[]>($"地址超出数据区范围: {address}");

            return OperationResult.CreateSuccessResult(
                DataStore.ReadBits(parsed.DeviceKind, parsed.Address, points));
        }

        private OperationResult WriteBitsCore(string address, bool[] values)
        {
            if (values == null || values.Length == 0)
                return OperationResult.CreateFailedResult("写入数据不能为空");

            bool isBitDevice;
            var addressResult = ParseAddress(address, out isBitDevice);
            if (!addressResult.IsSuccess)
                return OperationResult.CreateFailedResult(addressResult.Message);
            if (!isBitDevice)
                return OperationResult.CreateFailedResult("字软元件不支持位写入");

            McAddress parsed = addressResult.ResultValue;
            if (!DataStore.CanAccess(parsed.DeviceKind, parsed.Address, values.Length))
                return OperationResult.CreateFailedResult($"地址超出数据区范围: {address}");

            DataStore.WriteBits(parsed.DeviceKind, parsed.Address, values);
            OnDataWritten();
            return OperationResult.CreateSuccessResult();
        }

        private static OperationResult<T> Failed<T>(OperationResult source)
        {
            return OperationResult.CreateFailedResult<T>(source);
        }

        private static OperationResult<T> Failed<T>(string message)
        {
            return OperationResult.CreateFailedResult<T>(message);
        }

        private static OperationResult<T> First<T>(OperationResult<T[]> source)
        {
            if (!source.IsSuccess)
                return Failed<T>(source);

            return OperationResult.CreateSuccessResult(source.ResultValue[0]);
        }

        #endregion

        #region 读取

        /// <inheritdoc />
        public OperationResult<Dictionary<string, (DataTypeEnums, object)>> BatchRead(Dictionary<string, DataTypeEnums> addresses)
        {
            var result = new Dictionary<string, (DataTypeEnums, object)>();
            if (addresses == null)
                return OperationResult.CreateSuccessResult(result);

            var errors = new List<string>();
            foreach (var item in addresses)
            {
                var value = Read(item.Value, item.Key, 1);
                if (value.IsSuccess)
                {
                    result[item.Key] = (item.Value, value.ResultValue);
                }
                else
                {
                    errors.Add($"{item.Key}: {value.Message}");
                    result[item.Key] = (item.Value, null);
                }
            }

            if (errors.Count > 0)
            {
                var failed = OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>(
                    string.Join("; ", errors));
                failed.ResultValue = result;
                return failed;
            }

            return OperationResult.CreateSuccessResult(result);
        }

        /// <inheritdoc />
        public OperationResult<byte> ReadByte(string address)
        {
            var result = ReadWordsCore(address, 1);
            return result.IsSuccess
                ? OperationResult.CreateSuccessResult(result.ResultValue[0])
                : Failed<byte>(result);
        }

        /// <inheritdoc />
        public OperationResult<byte[]> ReadByte(string address, int length)
        {
            return ReadWordsCore(address, length);
        }

        /// <inheritdoc />
        public OperationResult<bool> ReadBoolean(string address)
        {
            var result = ReadBitsCore(address, 1);
            return result.IsSuccess
                ? OperationResult.CreateSuccessResult(result.ResultValue[0])
                : Failed<bool>(result);
        }

        /// <inheritdoc />
        public OperationResult<bool[]> ReadBoolean(string address, int length)
        {
            return ReadBitsCore(address, length);
        }

        /// <inheritdoc />
        public OperationResult<ushort> ReadUInt16(string address)
        {
            return First(ReadUInt16(address, 1));
        }

        /// <inheritdoc />
        public OperationResult<ushort[]> ReadUInt16(string address, int length)
        {
            var result = ReadWordsCore(address, 2 * length);
            return result.IsSuccess
                ? OperationResult.CreateSuccessResult(result.ResultValue.ToUInt16(0, length, IsReverse))
                : Failed<ushort[]>(result);
        }

        /// <inheritdoc />
        public OperationResult<short> ReadInt16(string address)
        {
            return First(ReadInt16(address, 1));
        }

        /// <inheritdoc />
        public OperationResult<short[]> ReadInt16(string address, int length)
        {
            var result = ReadWordsCore(address, 2 * length);
            return result.IsSuccess
                ? OperationResult.CreateSuccessResult(result.ResultValue.ToInt16(0, length, IsReverse))
                : Failed<short[]>(result);
        }

        /// <inheritdoc />
        public OperationResult<uint> ReadUInt32(string address)
        {
            return First(ReadUInt32(address, 1));
        }

        /// <inheritdoc />
        public OperationResult<uint[]> ReadUInt32(string address, int length)
        {
            var result = ReadWordsCore(address, 4 * length);
            return result.IsSuccess
                ? OperationResult.CreateSuccessResult(result.ResultValue.ToUInt32(0, length, DataFormat))
                : Failed<uint[]>(result);
        }

        /// <inheritdoc />
        public OperationResult<int> ReadInt32(string address)
        {
            return First(ReadInt32(address, 1));
        }

        /// <inheritdoc />
        public OperationResult<int[]> ReadInt32(string address, int length)
        {
            var result = ReadWordsCore(address, 4 * length);
            return result.IsSuccess
                ? OperationResult.CreateSuccessResult(result.ResultValue.ToInt32(0, length, DataFormat))
                : Failed<int[]>(result);
        }

        /// <inheritdoc />
        public OperationResult<ulong> ReadUInt64(string address)
        {
            return First(ReadUInt64(address, 1));
        }

        /// <inheritdoc />
        public OperationResult<ulong[]> ReadUInt64(string address, int length)
        {
            var result = ReadWordsCore(address, 8 * length);
            return result.IsSuccess
                ? OperationResult.CreateSuccessResult(result.ResultValue.ToUInt64(0, length, DataFormat))
                : Failed<ulong[]>(result);
        }

        /// <inheritdoc />
        public OperationResult<long> ReadInt64(string address)
        {
            return First(ReadInt64(address, 1));
        }

        /// <inheritdoc />
        public OperationResult<long[]> ReadInt64(string address, int length)
        {
            var result = ReadWordsCore(address, 8 * length);
            return result.IsSuccess
                ? OperationResult.CreateSuccessResult(result.ResultValue.ToInt64(0, length, DataFormat))
                : Failed<long[]>(result);
        }

        /// <inheritdoc />
        public OperationResult<float> ReadFloat(string address)
        {
            return First(ReadFloat(address, 1));
        }

        /// <inheritdoc />
        public OperationResult<float[]> ReadFloat(string address, int length)
        {
            var result = ReadWordsCore(address, 4 * length);
            return result.IsSuccess
                ? OperationResult.CreateSuccessResult(result.ResultValue.ToFloat(0, length, DataFormat))
                : Failed<float[]>(result);
        }

        /// <inheritdoc />
        public OperationResult<double> ReadDouble(string address)
        {
            return First(ReadDouble(address, 1));
        }

        /// <inheritdoc />
        public OperationResult<double[]> ReadDouble(string address, int length)
        {
            var result = ReadWordsCore(address, 8 * length);
            return result.IsSuccess
                ? OperationResult.CreateSuccessResult(result.ResultValue.ToDouble(0, length, DataFormat))
                : Failed<double[]>(result);
        }

        /// <inheritdoc />
        public OperationResult<string> ReadString(string address, int length)
        {
            var result = ReadWordsCore(address, length);
            if (!result.IsSuccess)
                return Failed<string>(result);

            return OperationResult.CreateSuccessResult<string>(Encoding.ASCII.GetString(result.ResultValue));
        }

        /// <inheritdoc />
        public OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address)
        {
            return Read(dataTypeEnum, address, 1);
        }

        /// <inheritdoc />
        public OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address, int length)
        {
            switch (dataTypeEnum)
            {
                case DataTypeEnums.Bool: return ToObject(ReadBoolean(address, length));
                case DataTypeEnums.Byte: return ToObject(ReadByte(address, length));
                case DataTypeEnums.Int16: return ToObject(ReadInt16(address, length));
                case DataTypeEnums.UInt16: return ToObject(ReadUInt16(address, length));
                case DataTypeEnums.Int32: return ToObject(ReadInt32(address, length));
                case DataTypeEnums.UInt32: return ToObject(ReadUInt32(address, length));
                case DataTypeEnums.Int64: return ToObject(ReadInt64(address, length));
                case DataTypeEnums.UInt64: return ToObject(ReadUInt64(address, length));
                case DataTypeEnums.Float: return ToObject(ReadFloat(address, length));
                case DataTypeEnums.Double: return ToObject(ReadDouble(address, length));
                case DataTypeEnums.String: return ToObject(ReadString(address, length));
                default: return Failed<object>($"不支持的数据类型: {dataTypeEnum}");
            }
        }

        private static OperationResult<object> ToObject<T>(OperationResult<T> source)
        {
            return source.IsSuccess
                ? OperationResult.CreateSuccessResult<object>(source.ResultValue)
                : Failed<object>(source);
        }

        #endregion

        #region 异步读取

        /// <inheritdoc />
        public ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(
            Dictionary<string, DataTypeEnums> addresses, CancellationToken cancellationToken = default(CancellationToken))
        {
            return new ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>>(BatchRead(addresses));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<byte>> ReadByteAsync(string address)
        {
            return new ValueTask<OperationResult<byte>>(ReadByte(address));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<byte[]>> ReadByteAsync(string address, int length)
        {
            return new ValueTask<OperationResult<byte[]>>(ReadByte(address, length));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<bool>> ReadBooleanAsync(string address)
        {
            return new ValueTask<OperationResult<bool>>(ReadBoolean(address));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<bool[]>> ReadBooleanAsync(string address, int length)
        {
            return new ValueTask<OperationResult<bool[]>>(ReadBoolean(address, length));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<ushort>> ReadUInt16Async(string address)
        {
            return new ValueTask<OperationResult<ushort>>(ReadUInt16(address));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<ushort[]>> ReadUInt16Async(string address, int length)
        {
            return new ValueTask<OperationResult<ushort[]>>(ReadUInt16(address, length));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<short>> ReadInt16Async(string address)
        {
            return new ValueTask<OperationResult<short>>(ReadInt16(address));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<short[]>> ReadInt16Async(string address, int length)
        {
            return new ValueTask<OperationResult<short[]>>(ReadInt16(address, length));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<uint>> ReadUInt32Async(string address)
        {
            return new ValueTask<OperationResult<uint>>(ReadUInt32(address));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<uint[]>> ReadUInt32Async(string address, int length)
        {
            return new ValueTask<OperationResult<uint[]>>(ReadUInt32(address, length));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<int>> ReadInt32Async(string address)
        {
            return new ValueTask<OperationResult<int>>(ReadInt32(address));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<int[]>> ReadInt32Async(string address, int length)
        {
            return new ValueTask<OperationResult<int[]>>(ReadInt32(address, length));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<ulong>> ReadUInt64Async(string address)
        {
            return new ValueTask<OperationResult<ulong>>(ReadUInt64(address));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<ulong[]>> ReadUInt64Async(string address, int length)
        {
            return new ValueTask<OperationResult<ulong[]>>(ReadUInt64(address, length));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<long>> ReadInt64Async(string address)
        {
            return new ValueTask<OperationResult<long>>(ReadInt64(address));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<long[]>> ReadInt64Async(string address, int length)
        {
            return new ValueTask<OperationResult<long[]>>(ReadInt64(address, length));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<float>> ReadFloatAsync(string address)
        {
            return new ValueTask<OperationResult<float>>(ReadFloat(address));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<float[]>> ReadFloatAsync(string address, int length)
        {
            return new ValueTask<OperationResult<float[]>>(ReadFloat(address, length));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<double>> ReadDoubleAsync(string address)
        {
            return new ValueTask<OperationResult<double>>(ReadDouble(address));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<double[]>> ReadDoubleAsync(string address, int length)
        {
            return new ValueTask<OperationResult<double[]>>(ReadDouble(address, length));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<string>> ReadStringAsync(string address, int length,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return new ValueTask<OperationResult<string>>(ReadString(address, length));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return new ValueTask<OperationResult<object>>(Read(dataTypeEnum, address));
        }

        /// <inheritdoc />
        public ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address, int length,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return new ValueTask<OperationResult<object>>(Read(dataTypeEnum, address, length));
        }

        #endregion

        #region 写入

        /// <inheritdoc />
        public OperationResult BatchWrite(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            if (addresses == null || addresses.Count == 0)
                return OperationResult.CreateSuccessResult();

            var errors = new List<string>();
            foreach (var item in addresses)
            {
                var result = Write(item.Value.Item1, item.Key, item.Value.Item2);
                if (!result.IsSuccess)
                    errors.Add($"{item.Key}: {result.Message}");
            }

            return errors.Count == 0
                ? OperationResult.CreateSuccessResult()
                : OperationResult.CreateFailedResult(string.Join("; ", errors));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, byte[] value)
        {
            return WriteWordsCore(address, value);
        }

        /// <inheritdoc />
        public OperationResult Write(string address, bool value)
        {
            return WriteBitsCore(address, new[] { value });
        }

        /// <inheritdoc />
        public OperationResult Write(string address, bool[] value)
        {
            return WriteBitsCore(address, value);
        }

        /// <inheritdoc />
        public OperationResult Write(string address, byte value)
        {
            return WriteWordsCore(address, new[] { value });
        }

        /// <inheritdoc />
        public OperationResult Write(string address, ushort value)
        {
            return WriteWordsCore(address, new[] { value }.ToByte(IsReverse));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, ushort[] value)
        {
            return WriteWordsCore(address, value.ToByte(IsReverse));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, short value)
        {
            return WriteWordsCore(address, new[] { value }.ToByte(IsReverse));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, short[] value)
        {
            return WriteWordsCore(address, value.ToByte(IsReverse));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, uint value)
        {
            return WriteWordsCore(address, new[] { value }.ToByte(DataFormat));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, uint[] value)
        {
            return WriteWordsCore(address, value.ToByte(DataFormat));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, int value)
        {
            return WriteWordsCore(address, new[] { value }.ToByte(DataFormat));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, int[] value)
        {
            return WriteWordsCore(address, value.ToByte(DataFormat));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, ulong value)
        {
            return WriteWordsCore(address, new[] { value }.ToByte(DataFormat));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, ulong[] value)
        {
            return WriteWordsCore(address, value.ToByte(DataFormat));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, long value)
        {
            return WriteWordsCore(address, new[] { value }.ToByte(DataFormat));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, long[] value)
        {
            return WriteWordsCore(address, value.ToByte(DataFormat));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, float value)
        {
            return WriteWordsCore(address, new[] { value }.ToByte(DataFormat));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, float[] value)
        {
            return WriteWordsCore(address, value.ToByte(DataFormat));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, double value)
        {
            return WriteWordsCore(address, new[] { value }.ToByte(DataFormat));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, double[] value)
        {
            return WriteWordsCore(address, value.ToByte(DataFormat));
        }

        /// <inheritdoc />
        public OperationResult Write(string address, string value)
        {
            return WriteWordsCore(address, Encoding.ASCII.GetBytes(value ?? string.Empty));
        }

        /// <inheritdoc />
        public OperationResult Write(DataTypeEnums dataTypeEnum, string address, object value)
        {
            if (value == null)
                return OperationResult.CreateFailedResult("写入值不能为空");

            try
            {
                switch (dataTypeEnum)
                {
                    case DataTypeEnums.Bool: return Write(address, Convert.ToBoolean(value));
                    case DataTypeEnums.Byte: return Write(address, Convert.ToByte(value));
                    case DataTypeEnums.Int16: return Write(address, Convert.ToInt16(value));
                    case DataTypeEnums.UInt16: return Write(address, Convert.ToUInt16(value));
                    case DataTypeEnums.Int32: return Write(address, Convert.ToInt32(value));
                    case DataTypeEnums.UInt32: return Write(address, Convert.ToUInt32(value));
                    case DataTypeEnums.Int64: return Write(address, Convert.ToInt64(value));
                    case DataTypeEnums.UInt64: return Write(address, Convert.ToUInt64(value));
                    case DataTypeEnums.Float: return Write(address, Convert.ToSingle(value));
                    case DataTypeEnums.Double: return Write(address, Convert.ToDouble(value));
                    case DataTypeEnums.String: return Write(address, value.ToString());
                    default: return OperationResult.CreateFailedResult($"不支持的数据类型: {dataTypeEnum}");
                }
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                return OperationResult.CreateFailedResult($"写入值转换失败: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public OperationResult Write(DataTypeEnums dataTypeEnum, string address, object[] value)
        {
            if (value == null || value.Length == 0)
                return OperationResult.CreateFailedResult("写入数据不能为空");

            try
            {
                switch (dataTypeEnum)
                {
                    case DataTypeEnums.Bool: return Write(address, Array.ConvertAll(value, Convert.ToBoolean));
                    case DataTypeEnums.Byte: return Write(address, Array.ConvertAll(value, Convert.ToByte));
                    case DataTypeEnums.Int16: return Write(address, Array.ConvertAll(value, Convert.ToInt16));
                    case DataTypeEnums.UInt16: return Write(address, Array.ConvertAll(value, Convert.ToUInt16));
                    case DataTypeEnums.Int32: return Write(address, Array.ConvertAll(value, Convert.ToInt32));
                    case DataTypeEnums.UInt32: return Write(address, Array.ConvertAll(value, Convert.ToUInt32));
                    case DataTypeEnums.Int64: return Write(address, Array.ConvertAll(value, Convert.ToInt64));
                    case DataTypeEnums.UInt64: return Write(address, Array.ConvertAll(value, Convert.ToUInt64));
                    case DataTypeEnums.Float: return Write(address, Array.ConvertAll(value, Convert.ToSingle));
                    case DataTypeEnums.Double: return Write(address, Array.ConvertAll(value, Convert.ToDouble));
                    default: return OperationResult.CreateFailedResult($"不支持的数据类型: {dataTypeEnum}");
                }
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                return OperationResult.CreateFailedResult($"写入值转换失败: {ex.Message}");
            }
        }

        #endregion

        #region 异步写入

        /// <inheritdoc />
        public ValueTask<OperationResult> BatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return new ValueTask<OperationResult>(BatchWrite(addresses));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, byte[] value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, bool value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, byte value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, ushort value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, ushort[] value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, short value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, short[] value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, uint value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, uint[] value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, int value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, int[] value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, ulong value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, ulong[] value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, long value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, long[] value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, float value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, float[] value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, double value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, double[] value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(string address, string value)
        {
            return Task.FromResult(Write(address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object value,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(Write(dataTypeEnum, address, value));
        }

        /// <inheritdoc />
        public Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object[] value,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(Write(dataTypeEnum, address, value));
        }

        #endregion
    }
}
