using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.Modbus.Data;

namespace Wombat.IndustrialCommunication.Extensions.Bluetooth.Modbus
{
    /// <summary>
    /// 基于蓝牙透传通道的 Modbus RTU 服务端。
    /// </summary>
    public class ModbusRtuBluetoothServer : ModbusRtuServerBase, IServer
    {
        private const int ModbusSnapshotVersion = 1;
        private const string ModbusSnapshotMagic = "WIC_MBSNAP";
        private readonly BluetoothServerAdapter _bluetoothServerAdapter;
        private readonly ServerMessageTransport _serverTransport;
        private readonly string _channelIdentity;
        private readonly object _snapshotSyncRoot = new object();
        private Timer _snapshotTimer;
        private volatile bool _snapshotDirty;
        private bool _enableSnapshotPersistence;

        public ModbusRtuBluetoothServer(IBluetoothChannel channel)
            : base(CreateTransport(channel))
        {
            _bluetoothServerAdapter = (BluetoothServerAdapter)base._transport.StreamResource;
            _serverTransport = base._transport;
            _channelIdentity = channel?.GetType().Name ?? "Bluetooth";
            InitializeSnapshotPersistence();
        }

        public override string Version => nameof(ModbusRtuBluetoothServer);

        public TimeSpan ConnectTimeout
        {
            get => _bluetoothServerAdapter.ConnectTimeout;
            set => _bluetoothServerAdapter.ConnectTimeout = value;
        }

        public TimeSpan ReceiveTimeout
        {
            get => _bluetoothServerAdapter.ReceiveTimeout;
            set => _bluetoothServerAdapter.ReceiveTimeout = value;
        }

        public TimeSpan SendTimeout
        {
            get => _bluetoothServerAdapter.SendTimeout;
            set => _bluetoothServerAdapter.SendTimeout = value;
        }

        public bool EnableSnapshotPersistence
        {
            get => _enableSnapshotPersistence;
            set
            {
                _enableSnapshotPersistence = value;
                if (value)
                {
                    StartSnapshotTimer();
                }
                else
                {
                    StopSnapshotTimer();
                }
            }
        }

        public string SnapshotFilePath { get; set; }

        public TimeSpan SnapshotSaveInterval { get; set; } = TimeSpan.FromSeconds(5);

        public new bool IsListening => base.IsListening;

        public OperationResult Listen()
        {
            if (EnableSnapshotPersistence)
            {
                TryLoadSnapshot();
                StartSnapshotTimer();
            }

            return StartAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public OperationResult Shutdown()
        {
            if (EnableSnapshotPersistence)
            {
                TrySaveSnapshot(true);
                StopSnapshotTimer();
            }

            return StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public void UseLogger(ILogger logger)
        {
            Logger = logger;
            _bluetoothServerAdapter.Logger = logger;
        }

        public void ConfigureSnapshotPersistence(string name = null)
        {
            SnapshotFilePath = BuildSnapshotFilePath("ModbusRtuBluetoothServer", _channelIdentity, name);
        }

        public OperationResult DeleteSnapshot()
        {
            try
            {
                lock (_snapshotSyncRoot)
                {
                    DeleteSnapshotFile(SnapshotFilePath);
                    _snapshotDirty = false;
                }

                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "删除Modbus RTU蓝牙快照失败: {SnapshotFilePath}", SnapshotFilePath);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }

        public OperationResult ResetDataAndDeleteSnapshot()
        {
            try
            {
                lock (_snapshotSyncRoot)
                {
                    ResetDataStore();
                    DeleteSnapshotFile(SnapshotFilePath);
                    _snapshotDirty = false;
                }

                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "重置Modbus RTU蓝牙数据并删除快照失败: {SnapshotFilePath}", SnapshotFilePath);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }

        public event EventHandler<DataStoreEventArgs> DataWritten
        {
            add { DataStore.DataStoreWrittenTo += value; }
            remove { DataStore.DataStoreWrittenTo -= value; }
        }

        public event EventHandler<DataStoreEventArgs> DataRead
        {
            add { DataStore.DataStoreReadFrom += value; }
            remove { DataStore.DataStoreReadFrom -= value; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopSnapshotTimer();
                _snapshotTimer?.Dispose();
                Shutdown();
                _serverTransport?.Dispose();
            }

            base.Dispose(disposing);
        }

        private static ServerMessageTransport CreateTransport(IBluetoothChannel channel)
        {
            var adapter = new BluetoothServerAdapter(channel);
            return new ServerMessageTransport(adapter);
        }

        private void InitializeSnapshotPersistence()
        {
            ConfigureSnapshotPersistence();
            DataStore.DataStoreWrittenTo += HandleSnapshotDataWritten;
        }

        private void HandleSnapshotDataWritten(object sender, DataStoreEventArgs e)
        {
            if (!EnableSnapshotPersistence)
            {
                return;
            }

            _snapshotDirty = true;
        }

        private void StartSnapshotTimer()
        {
            if (!EnableSnapshotPersistence)
            {
                return;
            }

            var interval = SnapshotSaveInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : SnapshotSaveInterval;
            lock (_snapshotSyncRoot)
            {
                if (_snapshotTimer == null)
                {
                    _snapshotTimer = new Timer(_ => TrySaveSnapshot(false), null, interval, interval);
                    return;
                }

                _snapshotTimer.Change(interval, interval);
            }
        }

        private void StopSnapshotTimer()
        {
            lock (_snapshotSyncRoot)
            {
                _snapshotTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void TryLoadSnapshot()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SnapshotFilePath))
                {
                    return;
                }

                LoadModbusSnapshot(SnapshotFilePath, DataStore);
                _snapshotDirty = false;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "加载Modbus RTU蓝牙快照失败: {SnapshotFilePath}", SnapshotFilePath);
            }
        }

        private void TrySaveSnapshot(bool force)
        {
            try
            {
                SaveSnapshot(force);
            }
            catch (Exception ex)
            {
                _snapshotDirty = true;
                Logger?.LogError(ex, "保存Modbus RTU蓝牙快照失败: {SnapshotFilePath}", SnapshotFilePath);
            }
        }

        private void SaveSnapshot(bool force)
        {
            if (!EnableSnapshotPersistence || string.IsNullOrWhiteSpace(SnapshotFilePath))
            {
                return;
            }

            lock (_snapshotSyncRoot)
            {
                if (!force && !_snapshotDirty)
                {
                    return;
                }

                SaveModbusSnapshot(SnapshotFilePath, DataStore);
                _snapshotDirty = false;
            }
        }

        private void ResetDataStore()
        {
            lock (DataStore.SyncRoot)
            {
                if (DataStore.CoilDiscretes != null)
                {
                    for (var index = 0; index < DataStore.CoilDiscretes.Size; index++)
                    {
                        DataStore.CoilDiscretes[index] = false;
                    }
                }

                if (DataStore.InputDiscretes != null)
                {
                    for (var index = 0; index < DataStore.InputDiscretes.Size; index++)
                    {
                        DataStore.InputDiscretes[index] = false;
                    }
                }

                if (DataStore.HoldingRegisters != null)
                {
                    for (var index = 0; index < DataStore.HoldingRegisters.Size; index++)
                    {
                        DataStore.HoldingRegisters[index] = 0;
                    }
                }

                if (DataStore.InputRegisters != null)
                {
                    for (var index = 0; index < DataStore.InputRegisters.Size; index++)
                    {
                        DataStore.InputRegisters[index] = 0;
                    }
                }
            }
        }

        private static string BuildSnapshotFilePath(string serverType, string endpointIdentity, string channelId = null)
        {
            var normalizedEndpointIdentity = SanitizeFileName(endpointIdentity);
            if (!string.IsNullOrWhiteSpace(channelId))
            {
                var normalizedChannelId = SanitizeFileName(channelId);
                return Path.Combine(AppContext.BaseDirectory, "Snapshots", $"{serverType}_{normalizedChannelId}_{normalizedEndpointIdentity}.snapshot");
            }

            return Path.Combine(AppContext.BaseDirectory, "Snapshots", $"{serverType}_{normalizedEndpointIdentity}.snapshot");
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "default";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
        }

        private static void SaveModbusSnapshot(string filePath, DataStore dataStore)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (dataStore == null)
                throw new ArgumentNullException(nameof(dataStore));

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempFilePath = filePath + ".tmp";
            lock (dataStore.SyncRoot)
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(ModbusSnapshotMagic);
                    writer.Write(ModbusSnapshotVersion);
                    WriteBoolMemory(writer, dataStore.CoilDiscretes);
                    WriteBoolMemory(writer, dataStore.InputDiscretes);
                    WriteIntMemory(writer, dataStore.HoldingRegisters);
                    WriteIntMemory(writer, dataStore.InputRegisters);
                }
            }

            ReplaceFile(tempFilePath, filePath);
        }

        private static void LoadModbusSnapshot(string filePath, DataStore dataStore)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (dataStore == null)
                throw new ArgumentNullException(nameof(dataStore));
            if (!File.Exists(filePath))
                return;

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream))
            {
                var magic = reader.ReadString();
                if (!string.Equals(magic, ModbusSnapshotMagic, StringComparison.Ordinal))
                    throw new InvalidDataException("无效的Modbus快照文件");

                var version = reader.ReadInt32();
                if (version != ModbusSnapshotVersion)
                    throw new InvalidDataException($"不支持的Modbus快照版本: {version}");

                var coils = ReadBoolMemory(reader);
                var inputs = ReadBoolMemory(reader);
                var holdingRegisters = ReadIntMemory(reader);
                var inputRegisters = ReadIntMemory(reader);

                lock (dataStore.SyncRoot)
                {
                    dataStore.CoilDiscretes = coils;
                    dataStore.InputDiscretes = inputs;
                    dataStore.HoldingRegisters = holdingRegisters;
                    dataStore.InputRegisters = inputRegisters;
                }
            }
        }

        private static void DeleteSnapshotFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            var tempFilePath = filePath + ".tmp";
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }

        private static void ReplaceFile(string tempFilePath, string targetFilePath)
        {
            if (File.Exists(targetFilePath))
            {
                File.Delete(targetFilePath);
            }

            File.Move(tempFilePath, targetFilePath);
        }

        private static void WriteBoolMemory(BinaryWriter writer, MemoryLite<bool> memory)
        {
            var values = ToArray(memory);
            writer.Write(values.Length);
            for (var index = 0; index < values.Length; index++)
            {
                writer.Write(values[index]);
            }
        }

        private static MemoryLite<bool> ReadBoolMemory(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            var values = new bool[length];
            for (var index = 0; index < length; index++)
            {
                values[index] = reader.ReadBoolean();
            }

            return new MemoryLite<bool>(values, 0, length);
        }

        private static void WriteIntMemory(BinaryWriter writer, MemoryLite<int> memory)
        {
            var values = ToArray(memory);
            writer.Write(values.Length);
            for (var index = 0; index < values.Length; index++)
            {
                writer.Write(values[index]);
            }
        }

        private static MemoryLite<int> ReadIntMemory(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            var values = new int[length];
            for (var index = 0; index < length; index++)
            {
                values[index] = reader.ReadInt32();
            }

            return new MemoryLite<int>(values, 0, length);
        }

        private static T[] ToArray<T>(MemoryLite<T> memory)
        {
            if (memory == null)
            {
                return Array.Empty<T>();
            }

            var values = new T[memory.Size];
            for (var index = 0; index < memory.Size; index++)
            {
                values[index] = memory[index];
            }

            return values;
        }
    }
}
