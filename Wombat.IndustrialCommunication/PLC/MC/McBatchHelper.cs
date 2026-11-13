using System;
using System.Collections.Generic;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC 批量读写的地址解析与块合并。纯计算，不做任何 I/O。
    /// 合并只允许同软元件、连续编号、且每元素占 1 字的场景，避免跨软元件或长定时器越界。
    /// </summary>
    public static class McBatchHelper
    {
        /// <summary>单个地址的批量信息</summary>
        public sealed class McBatchAddressInfo
        {
            /// <summary>用户书写的原始地址</summary>
            public string OriginalAddress { get; set; }

            /// <summary>解析后的地址</summary>
            public McAddress Address { get; set; }

            /// <summary>数据类型</summary>
            public DataTypeEnums DataType { get; set; }

            /// <summary>占用的字数（位访问为点数）</summary>
            public int WordCount { get; set; }

            /// <summary>是否位访问</summary>
            public bool IsBit { get; set; }
        }

        /// <summary>合并后的块</summary>
        public sealed class McBatchBlock
        {
            /// <summary>块起始地址</summary>
            public McAddress Start { get; set; }

            /// <summary>块占用的字数（位访问为点数）</summary>
            public int WordCount { get; set; }

            /// <summary>是否位访问</summary>
            public bool IsBit { get; set; }

            /// <summary>块内地址</summary>
            public List<McBatchAddressInfo> Addresses { get; } = new List<McBatchAddressInfo>();

            /// <summary>块内每个地址在块内的字偏移</summary>
            public List<int> Offsets { get; } = new List<int>();
        }

        /// <summary>取数据类型占用的字数；返回 0 表示批量不支持该类型</summary>
        public static int GetWordCount(DataTypeEnums dataType)
        {
            switch (dataType)
            {
                case DataTypeEnums.Bool: return 1;
                case DataTypeEnums.Byte: return 1;
                case DataTypeEnums.Int16: return 1;
                case DataTypeEnums.UInt16: return 1;
                case DataTypeEnums.Int32: return 2;
                case DataTypeEnums.UInt32: return 2;
                case DataTypeEnums.Float: return 2;
                case DataTypeEnums.Int64: return 4;
                case DataTypeEnums.UInt64: return 4;
                case DataTypeEnums.Double: return 4;
                default: return 0;
            }
        }

        /// <summary>解析地址列表，任一地址非法即整体失败</summary>
        public static bool TryParseAddresses(Dictionary<string, DataTypeEnums> addresses, McProfile profile,
            out List<McBatchAddressInfo> result, out string error)
        {
            result = new List<McBatchAddressInfo>();
            error = null;

            if (addresses == null || addresses.Count == 0)
            {
                error = "地址列表不能为空";
                return false;
            }

            foreach (var pair in addresses)
            {
                McAddress address;
                try
                {
                    address = McAddress.Parse(pair.Key, profile);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException ||
                                           ex is OverflowException)
                {
                    error = ex.Message;
                    result.Clear();
                    return false;
                }

                int wordCount = GetWordCount(pair.Value);
                if (wordCount == 0)
                {
                    error = $"地址 {pair.Key} 的数据类型 {pair.Value} 不支持批量访问";
                    result.Clear();
                    return false;
                }

                if (pair.Value == DataTypeEnums.Bool && !address.IsBitDevice)
                {
                    error = $"地址 {pair.Key} 是字软元件，不能用 Bool 批量读取，请改用 UInt16 等类型";
                    result.Clear();
                    return false;
                }

                if (pair.Value != DataTypeEnums.Bool && address.IsBitDevice)
                {
                    error = $"地址 {pair.Key} 是位软元件，只能用 Bool 批量读取";
                    result.Clear();
                    return false;
                }

                result.Add(new McBatchAddressInfo
                {
                    OriginalAddress = pair.Key,
                    Address = address,
                    DataType = pair.Value,
                    WordCount = pair.Value == DataTypeEnums.Bool ? 1 : wordCount * address.WordsPerPoint,
                    IsBit = pair.Value == DataTypeEnums.Bool
                });
            }

            return true;
        }

        /// <summary>把地址按软元件与连续性合并成块，并按帧上限切分</summary>
        public static List<McBatchBlock> BuildBlocks(List<McBatchAddressInfo> addresses, McProfile profile)
        {
            if (addresses == null)
                throw new ArgumentNullException(nameof(addresses));

            var blocks = new List<McBatchBlock>();
            McBatchBlock current = null;

            foreach (var info in addresses)
            {
                if (current != null)
                {
                    int limit = profile.GetBatchMaxPoints(current.IsBit);
                    if (!CanMerge(current, info) || current.WordCount + info.WordCount > limit)
                        current = null;
                }

                if (current == null)
                {
                    current = new McBatchBlock
                    {
                        Start = info.Address,
                        IsBit = info.IsBit,
                        WordCount = 0
                    };
                    blocks.Add(current);
                }

                current.Offsets.Add(current.WordCount);
                current.Addresses.Add(info);
                current.WordCount += info.WordCount;
            }

            return blocks;
        }

        /// <summary>把值转换为协议字节序的负载</summary>
        public static byte[] ConvertValueToBytes(object value, DataTypeEnums dataType)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            switch (dataType)
            {
                case DataTypeEnums.Byte:
                    return new[] { Convert.ToByte(value), (byte)0 };
                case DataTypeEnums.Int16:
                    return BitConverter.GetBytes(Convert.ToInt16(value));
                case DataTypeEnums.UInt16:
                    return BitConverter.GetBytes(Convert.ToUInt16(value));
                case DataTypeEnums.Int32:
                    return BitConverter.GetBytes(Convert.ToInt32(value));
                case DataTypeEnums.UInt32:
                    return BitConverter.GetBytes(Convert.ToUInt32(value));
                case DataTypeEnums.Float:
                    return BitConverter.GetBytes(Convert.ToSingle(value));
                case DataTypeEnums.Int64:
                    return BitConverter.GetBytes(Convert.ToInt64(value));
                case DataTypeEnums.UInt64:
                    return BitConverter.GetBytes(Convert.ToUInt64(value));
                case DataTypeEnums.Double:
                    return BitConverter.GetBytes(Convert.ToDouble(value));
                default:
                    throw new NotSupportedException($"批量写入不支持数据类型 {dataType}");
            }
        }

        /// <summary>从块数据中按偏移取出单个值</summary>
        public static object ExtractValue(byte[] data, int wordOffset, DataTypeEnums dataType)
        {
            int offset = wordOffset * 2;
            switch (dataType)
            {
                case DataTypeEnums.Byte:
                    return data[offset];
                case DataTypeEnums.Int16:
                    return BitConverter.ToInt16(data, offset);
                case DataTypeEnums.UInt16:
                    return BitConverter.ToUInt16(data, offset);
                case DataTypeEnums.Int32:
                    return BitConverter.ToInt32(data, offset);
                case DataTypeEnums.UInt32:
                    return BitConverter.ToUInt32(data, offset);
                case DataTypeEnums.Float:
                    return BitConverter.ToSingle(data, offset);
                case DataTypeEnums.Int64:
                    return BitConverter.ToInt64(data, offset);
                case DataTypeEnums.UInt64:
                    return BitConverter.ToUInt64(data, offset);
                case DataTypeEnums.Double:
                    return BitConverter.ToDouble(data, offset);
                default:
                    throw new NotSupportedException($"批量读取不支持数据类型 {dataType}");
            }
        }

        private static bool CanMerge(McBatchBlock block, McBatchAddressInfo info)
        {
            return CanMerge(block.Start.DeviceKind, block.IsBit, block.Start.Address + block.WordCount, info);
        }

        private static bool CanMerge(McDeviceKind kind, bool isBit, int expectedAddress, McBatchAddressInfo info)
        {
            if (info.IsBit != isBit)
                return false;
            if (info.Address.Device.Kind != kind)
                return false;

            // 长定时器（4 字/点）不做合并，避免块内字偏移与元素偏移不一致
            if (info.Address.WordsPerPoint != 1)
                return false;

            if (info.Address.Address != expectedAddress)
                return false;

            return true;
        }
    }
}
