using System;
using System.Collections.Generic;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC 服务端数据区：按归一化软元件种类（<see cref="McDeviceKind"/>）分区存储，
    /// 位区 1 字节/点，字区 2 字节/点，iQ-R 长定时器 4 字/点。
    /// 用种类而不是协议码做 key，保证 1E 与 3E/4E 客户端看到的是同一块内存。
    /// </summary>
    public sealed class McDataStore
    {
        /// <summary>位软元件默认点数</summary>
        public const int DefaultBitPoints = 8192;

        /// <summary>字软元件默认点数</summary>
        public const int DefaultWordPoints = 8192;

        /// <summary>长定时器/长计数器默认元素个数（每元素 4 字）</summary>
        public const int DefaultLongTimerPoints = 2048;

        private readonly object _syncRoot = new object();
        private readonly Dictionary<McDeviceKind, MemoryLite<byte>> _areas =
            new Dictionary<McDeviceKind, MemoryLite<byte>>();

        /// <summary>同步根</summary>
        public object SyncRoot
        {
            get { return _syncRoot; }
        }

        /// <summary>已分配的数据区种类</summary>
        public IReadOnlyList<McDeviceKind> Kinds
        {
            get
            {
                lock (_syncRoot)
                {
                    var kinds = new List<McDeviceKind>(_areas.Keys);
                    kinds.Sort();
                    return kinds;
                }
            }
        }

        /// <summary>
        /// 每个点占用的字节数：位软元件 1 字节/点，字软元件 2 × WordsPerPoint 字节/点。
        /// </summary>
        public static int GetBytesPerPoint(McDeviceKind kind)
        {
            McDeviceDescriptor descriptor = McDeviceDescriptor.Find(kind);
            return descriptor.IsBit ? 1 : descriptor.WordsPerPoint * 2;
        }

        /// <summary>取默认点数（长定时器为元素个数）</summary>
        public static int GetDefaultPoints(McDeviceKind kind)
        {
            McDeviceDescriptor descriptor = McDeviceDescriptor.Find(kind);
            if (descriptor.IsBit)
                return DefaultBitPoints;

            return descriptor.WordsPerPoint > 1 ? DefaultLongTimerPoints : DefaultWordPoints;
        }

        /// <summary>取数据区容量（位区为点数，长定时器为元素个数）</summary>
        public int GetCapacity(McDeviceKind kind)
        {
            lock (_syncRoot)
            {
                return GetArea(kind).Size / GetBytesPerPoint(kind);
            }
        }

        /// <summary>重建数据区容量（原有数据保留在重叠部分）</summary>
        public void Resize(McDeviceKind kind, int points)
        {
            if (points <= 0)
                throw new ArgumentOutOfRangeException(nameof(points), points, "数据区容量必须大于 0");

            int bytesPerPoint = GetBytesPerPoint(kind);
            lock (_syncRoot)
            {
                var existing = GetArea(kind);
                int length = points * bytesPerPoint;
                var buffer = new byte[length];
                int copy = Math.Min(length, existing.Size);
                existing.GetMemory().Span.Slice(0, copy).CopyTo(buffer);
                _areas[kind] = new MemoryLite<byte>(buffer, 0, length);
            }
        }

        /// <summary>
        /// 判断一次访问是否落在数据区内。
        /// </summary>
        /// <param name="kind">软元件种类</param>
        /// <param name="startPoint">起始编号（长定时器为元素编号）</param>
        /// <param name="points">协议点数（长定时器为字号数）</param>
        public bool CanAccess(McDeviceKind kind, int startPoint, int points)
        {
            if (startPoint < 0 || points < 0)
                return false;

            McDeviceDescriptor descriptor = McDeviceDescriptor.Find(kind);
            lock (_syncRoot)
            {
                int size = GetArea(kind).Size;
                int offset = startPoint * GetBytesPerPoint(kind);
                // 长定时器每元素 4 字，协议点数本身就是字数，直接按 2 字节/点算
                int length = descriptor.IsBit ? points : points * 2;
                return (long)offset + length <= size;
            }
        }

        /// <summary>读位数据</summary>
        public bool[] ReadBits(McDeviceKind kind, int startPoint, int points)
        {
            var descriptor = McDeviceDescriptor.Find(kind);
            if (!descriptor.IsBit)
                throw new ArgumentException($"{kind} 是字软元件，不能按位读取", nameof(kind));

            var values = new bool[points];
            lock (_syncRoot)
            {
                var memory = GetArea(kind).GetMemory().Span;
                for (int i = 0; i < points; i++)
                {
                    values[i] = memory[startPoint + i] != 0;
                }
            }

            return values;
        }

        /// <summary>写位数据</summary>
        public void WriteBits(McDeviceKind kind, int startPoint, bool[] values)
        {
            var descriptor = McDeviceDescriptor.Find(kind);
            if (!descriptor.IsBit)
                throw new ArgumentException($"{kind} 是字软元件，不能按位写入", nameof(kind));
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            lock (_syncRoot)
            {
                var memory = GetArea(kind).GetMemory().Span;
                for (int i = 0; i < values.Length; i++)
                {
                    memory[startPoint + i] = values[i] ? (byte)1 : (byte)0;
                }
            }
        }

        /// <summary>
        /// 读字数据，返回协议字节（每点 2 字节小端；长定时器每元素 4 字）。
        /// </summary>
        public byte[] ReadWords(McDeviceKind kind, int startPoint, int points)
        {
            var descriptor = McDeviceDescriptor.Find(kind);
            if (descriptor.IsBit)
                throw new ArgumentException($"{kind} 是位软元件，不能按字读取", nameof(kind));

            var data = new byte[points * 2];
            lock (_syncRoot)
            {
                var memory = GetArea(kind).GetMemory().Span;
                int offset = startPoint * GetBytesPerPoint(kind);
                memory.Slice(offset, data.Length).CopyTo(data);
            }

            return data;
        }

        /// <summary>写字数据（每个协议点 2 字节）</summary>
        public void WriteWords(McDeviceKind kind, int startPoint, byte[] data)
        {
            var descriptor = McDeviceDescriptor.Find(kind);
            if (descriptor.IsBit)
                throw new ArgumentException($"{kind} 是位软元件，不能按字写入", nameof(kind));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            lock (_syncRoot)
            {
                var memory = GetArea(kind).GetMemory().Span;
                int offset = startPoint * GetBytesPerPoint(kind);
                data.AsSpan().CopyTo(memory.Slice(offset, data.Length));
            }
        }

        /// <summary>导出整个数据区（快照用）</summary>
        public byte[] GetAreaBytes(McDeviceKind kind)
        {
            lock (_syncRoot)
            {
                var memory = GetArea(kind);
                var bytes = new byte[memory.Size];
                memory.GetMemory().Span.CopyTo(bytes);
                return bytes;
            }
        }

        /// <summary>导入整个数据区（快照恢复用，容量随文件变化）</summary>
        public void SetAreaBytes(McDeviceKind kind, byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            lock (_syncRoot)
            {
                int bytesPerPoint = GetBytesPerPoint(kind);
                int usable = bytes.Length - bytes.Length % bytesPerPoint;
                var buffer = new byte[Math.Max(bytesPerPoint, usable)];
                Array.Copy(bytes, buffer, buffer.Length);
                _areas[kind] = new MemoryLite<byte>(buffer, 0, buffer.Length);
            }
        }

        /// <summary>清空全部数据区（容量不变）</summary>
        public void Reset()
        {
            lock (_syncRoot)
            {
                foreach (var area in _areas.Values)
                {
                    area.GetMemory().Span.Clear();
                }
            }
        }

        private MemoryLite<byte> GetArea(McDeviceKind kind)
        {
            MemoryLite<byte> memory;
            if (_areas.TryGetValue(kind, out memory))
                return memory;

            int length = GetDefaultPoints(kind) * GetBytesPerPoint(kind);
            memory = new MemoryLite<byte>(new byte[length], 0, length);
            _areas[kind] = memory;
            return memory;
        }
    }
}
