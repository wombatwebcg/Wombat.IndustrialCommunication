using System;
using System.Collections.Generic;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC 协议公共方法：位打包、字节序读写与结束码映射。全部为纯函数。
    /// </summary>
    public static class McCommonMethods
    {
        /// <summary>正常结束</summary>
        public const ushort EndCodeNormal = 0x0000;
        /// <summary>设备范围超限</summary>
        public const ushort EndCodeDeviceRangeExceeded = 0x4031;
        /// <summary>ASCII/二进制模式不匹配</summary>
        public const ushort EndCodeModeMismatch = 0xC050;
        /// <summary>点数超出允许范围</summary>
        public const ushort EndCodePointCountOutOfRange = 0xC051;
        /// <summary>请求超出最大地址</summary>
        public const ushort EndCodeRequestBeyondMaxAddress = 0xC056;
        /// <summary>请求数据长度错误</summary>
        public const ushort EndCodeRequestDataLengthError = 0xC058;
        /// <summary>指令/子指令不支持</summary>
        public const ushort EndCodeCommandNotSupported = 0xC059;
        /// <summary>软元件指定错误</summary>
        public const ushort EndCodeDeviceSpecificationError = 0xC05B;
        /// <summary>设备偏移错误</summary>
        public const ushort EndCodeDeviceOffsetError = 0xC05C;
        /// <summary>目标模块无法执行</summary>
        public const ushort EndCodeTargetModuleCannotExecute = 0xC05F;
        /// <summary>1E 的异常结束码（其后跟 1 字节异常码）</summary>
        public const byte EndCodeA1EAbnormal = 0x5B;

        /// <summary>结束码是否表示正常</summary>
        public static bool IsEndCodeSuccess(ushort endCode)
        {
            return endCode == EndCodeNormal;
        }

        /// <summary>取 3E/4E 结束码描述</summary>
        public static string GetEndCodeMessage(ushort endCode)
        {
            switch (endCode)
            {
                case EndCodeNormal: return "正常";
                case EndCodeDeviceRangeExceeded: return "设备范围超限";
                case EndCodeModeMismatch: return "ASCII/二进制模式不匹配";
                case EndCodePointCountOutOfRange: return "点数超出允许范围";
                case EndCodeRequestBeyondMaxAddress: return "请求超出最大地址";
                case EndCodeRequestDataLengthError: return "请求数据长度错误";
                case EndCodeCommandNotSupported: return "指令/子指令不支持";
                case EndCodeDeviceSpecificationError: return "软元件指定错误";
                case EndCodeDeviceOffsetError: return "设备偏移错误";
                case EndCodeTargetModuleCannotExecute: return "目标模块无法执行";
                default: return "未知结束码";
            }
        }

        /// <summary>取 1E 结束码描述</summary>
        public static string GetA1EEndCodeMessage(byte endCode)
        {
            switch (endCode)
            {
                case 0x00: return "正常";
                case EndCodeA1EAbnormal: return "异常结束";
                default: return "未知结束码";
            }
        }

        /// <summary>把结束码格式化为「0xC051 点数超出允许范围」</summary>
        public static string FormatEndCode(ushort endCode)
        {
            return $"0x{endCode:X4} {GetEndCodeMessage(endCode)}";
        }

        /// <summary>
        /// 把 3E/4E 结束码折算成 1E 的异常码（服务端生成 1E 错误响应时使用）。
        /// 手册只给出异常码表的大类，取值待真机核对；未知结束码统一回 0x10。
        /// </summary>
        public static byte MapA1EAbnormalCode(ushort endCode)
        {
            switch (endCode)
            {
                case EndCodePointCountOutOfRange: return 0x18;
                case EndCodeRequestBeyondMaxAddress: return 0x19;
                case EndCodeRequestDataLengthError: return 0x13;
                case EndCodeCommandNotSupported: return 0x11;
                case EndCodeDeviceSpecificationError: return 0x10;
                default: return 0x10;
            }
        }

        /// <summary>把 1E 结束码格式化为「0x5B 异常结束（异常码 0x10）」</summary>
        public static string FormatA1EEndCode(byte endCode, byte abnormalCode)
        {
            if (endCode == EndCodeA1EAbnormal)
                return $"0x{endCode:X2} {GetA1EEndCodeMessage(endCode)}（异常码 0x{abnormalCode:X2}）";

            return $"0x{endCode:X2} {GetA1EEndCodeMessage(endCode)}";
        }

        /// <summary>
        /// 位打包：每点 4 bit，第一点在高 4 位，奇数点时低 4 位补 0。
        /// </summary>
        public static byte[] PackBitData(IReadOnlyList<bool> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            var buffer = new byte[(values.Count + 1) / 2];
            for (int i = 0; i < values.Count; i++)
            {
                if (!values[i])
                    continue;

                if ((i & 1) == 0)
                    buffer[i / 2] |= 0x10;
                else
                    buffer[i / 2] |= 0x01;
            }

            return buffer;
        }

        /// <summary>
        /// 位解包：每点 4 bit，第一点在高 4 位，非 0 即为 ON。
        /// </summary>
        public static bool[] UnpackBitData(byte[] data, int points)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (points < 0)
                throw new ArgumentOutOfRangeException(nameof(points), points, "点数不能为负");

            int required = (points + 1) / 2;
            if (data.Length < required)
                throw new ArgumentException($"位数据长度不足：需要 {required} 字节，实际 {data.Length} 字节", nameof(data));

            var values = new bool[points];
            for (int i = 0; i < points; i++)
            {
                byte current = data[i / 2];
                int nibble = (i & 1) == 0 ? (current >> 4) & 0x0F : current & 0x0F;
                values[i] = nibble != 0;
            }

            return values;
        }

        /// <summary>读小端 UInt16</summary>
        public static ushort ReadUInt16(byte[] buffer, int offset)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset + 2 > buffer.Length)
                throw new ArgumentException($"读取 2 字节越界：offset={offset}, length={buffer.Length}", nameof(offset));

            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        /// <summary>写小端 UInt16</summary>
        public static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset + 2 > buffer.Length)
                throw new ArgumentException($"写入 2 字节越界：offset={offset}, length={buffer.Length}", nameof(offset));

            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)(value >> 8);
        }
    }
}
