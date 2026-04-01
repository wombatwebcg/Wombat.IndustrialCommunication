using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wombat.IndustrialCommunication.Modbus.Data;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication
{
    internal static class ServerSnapshotPersistence
    {
        private const int S7SnapshotVersion = 1;
        private const int ModbusSnapshotVersion = 1;
        private const string S7SnapshotMagic = "WIC_S7SNAP";
        private const string ModbusSnapshotMagic = "WIC_MBSNAP";

        public static void SaveS7Snapshot(string filePath, S7DataStore dataStore)
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
                    writer.Write(S7SnapshotMagic);
                    writer.Write(S7SnapshotVersion);
                    WriteByteMemory(writer, dataStore.Merkers);
                    WriteByteMemory(writer, dataStore.Inputs);
                    WriteByteMemory(writer, dataStore.Outputs);
                    WriteByteMemory(writer, dataStore.Timers);
                    WriteByteMemory(writer, dataStore.Counters);

                    writer.Write(dataStore.DataBlocks.Count);
                    foreach (var item in dataStore.DataBlocks.OrderBy(x => x.Key))
                    {
                        writer.Write(item.Key);
                        WriteByteMemory(writer, item.Value);
                    }
                }
            }

            ReplaceFile(tempFilePath, filePath);
        }

        public static void LoadS7Snapshot(string filePath, S7DataStore dataStore)
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
                if (!string.Equals(magic, S7SnapshotMagic, StringComparison.Ordinal))
                    throw new InvalidDataException("无效的S7快照文件");

                var version = reader.ReadInt32();
                if (version != S7SnapshotVersion)
                    throw new InvalidDataException($"不支持的S7快照版本: {version}");

                var merkers = ReadByteMemory(reader);
                var inputs = ReadByteMemory(reader);
                var outputs = ReadByteMemory(reader);
                var timers = ReadByteMemory(reader);
                var counters = ReadByteMemory(reader);
                var dataBlocks = new Dictionary<int, MemoryLite<byte>>();
                var count = reader.ReadInt32();
                for (var index = 0; index < count; index++)
                {
                    var dbNumber = reader.ReadInt32();
                    dataBlocks[dbNumber] = ReadByteMemory(reader);
                }

                lock (dataStore.SyncRoot)
                {
                    dataStore.Merkers = merkers;
                    dataStore.Inputs = inputs;
                    dataStore.Outputs = outputs;
                    dataStore.Timers = timers;
                    dataStore.Counters = counters;
                    dataStore.DataBlocks = dataBlocks;
                }
            }
        }

        public static void SaveModbusSnapshot(string filePath, DataStore dataStore)
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

        public static void LoadModbusSnapshot(string filePath, DataStore dataStore)
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

        public static void DeleteSnapshot(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

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

        private static void WriteByteMemory(BinaryWriter writer, MemoryLite<byte> memory)
        {
            var values = ToArray(memory);
            writer.Write(values.Length);
            writer.Write(values);
        }

        private static MemoryLite<byte> ReadByteMemory(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException();

            return new MemoryLite<byte>(bytes, 0, length);
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
                return Array.Empty<T>();

            var values = new T[memory.Size];
            for (var index = 0; index < memory.Size; index++)
            {
                values[index] = memory[index];
            }

            return values;
        }
    }
}
