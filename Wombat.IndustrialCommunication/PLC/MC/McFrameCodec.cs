using System;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC 帧编解码器：构造 1E / 3E / 4E 请求，解析三种响应。
    /// 客户端与服务端共用，纯函数、无 I/O、无 async。
    /// </summary>
    public static class McFrameCodec
    {
        /// <summary>批量读指令</summary>
        public const ushort CommandBatchRead = 0x0401;
        /// <summary>批量写指令</summary>
        public const ushort CommandBatchWrite = 0x1401;

        /// <summary>3E 请求副头部</summary>
        public const ushort SubHeader3E = 0x0050;
        /// <summary>3E 响应副头部</summary>
        public const ushort SubHeader3EResponse = 0x00D0;
        /// <summary>4E 请求副头部</summary>
        public const ushort SubHeader4E = 0x0054;
        /// <summary>4E 响应副头部</summary>
        public const ushort SubHeader4EResponse = 0x00D4;

        /// <summary>默认监视定时器（250ms × 10 = 2.5s）</summary>
        public const ushort DefaultMonitorTimer = 10;

        private static readonly byte[] Empty = new byte[0];

        /// <summary>
        /// 取响应帧头长度：传输层先读该长度即可拿到结束码与数据长度。
        /// 1E 为 2（副头部 + 结束码），3E 为 11，4E 为 15。
        /// </summary>
        public static int GetResponseHeaderLength(McFrameType frameType)
        {
            switch (frameType)
            {
                case McFrameType.A1E: return 2;
                case McFrameType.Q3E: return 11;
                case McFrameType.Q4E: return 15;
                default: throw new ArgumentOutOfRangeException(nameof(frameType), frameType, "未知的 MC 帧类型");
            }
        }

        /// <summary>构造批量读请求</summary>
        /// <param name="profile">帧/系列派生规则</param>
        /// <param name="address">起始软元件地址</param>
        /// <param name="points">协议点数（已由 <see cref="McAddress.GetPointCount"/> 换算）</param>
        /// <param name="serialNumber">4E 序列号，1E/3E 忽略</param>
        public static byte[] BuildReadRequest(McProfile profile, McAddress address, int points, ushort serialNumber = 0)
        {
            return BuildRequest(profile, address, points, null, serialNumber);
        }

        /// <summary>构造批量写请求</summary>
        /// <param name="profile">帧/系列派生规则</param>
        /// <param name="address">起始软元件地址</param>
        /// <param name="points">协议点数（已由 <see cref="McAddress.GetPointCount"/> 换算）</param>
        /// <param name="data">数据区（位：每点 4 bit；字：每点 2 字节小端）</param>
        /// <param name="serialNumber">4E 序列号，1E/3E 忽略</param>
        public static byte[] BuildWriteRequest(McProfile profile, McAddress address, int points, byte[] data, ushort serialNumber = 0)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("写入数据不能为空", nameof(data));

            return BuildRequest(profile, address, points, data, serialNumber);
        }

        /// <summary>
        /// 解析响应帧。帧结构非法时抛出 <see cref="ArgumentException"/>，
        /// 4E 序列号不匹配时抛出 <see cref="InvalidOperationException"/>（判定为帧错位）。
        /// 结束码非正常不抛异常，由 <see cref="McPacket.IsSuccess"/> 表达。
        /// </summary>
        public static McPacket ParseResponse(McFrameType frameType, byte[] frame, ushort? expectedSerialNumber = null)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));

            switch (frameType)
            {
                case McFrameType.A1E:
                    return ParseA1EResponse(frame);
                case McFrameType.Q3E:
                    return ParseQnaResponse(frame, false);
                case McFrameType.Q4E:
                    return ParseQnaResponse(frame, true, expectedSerialNumber);
                default:
                    throw new ArgumentOutOfRangeException(nameof(frameType), frameType, "未知的 MC 帧类型");
            }
        }

        private static byte[] BuildRequest(McProfile profile, McAddress address, int points, byte[] data, ushort serialNumber)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            bool isBit = address.IsBitDevice;
            int maxPoints = profile.GetMaxPoints(isBit);
            if (points <= 0 || points > maxPoints)
                throw new ArgumentOutOfRangeException(nameof(points), points,
                    $"{profile} 单帧点数必须在 1~{maxPoints} 之间");

            byte[] payload = data ?? Empty;
            if (data != null)
            {
                int expected = isBit ? (points + 1) / 2 : points * 2;
                if (payload.Length != expected)
                    throw new ArgumentException(
                        $"写入数据长度 {payload.Length} 字节与点数 {points} 不符（应为 {expected} 字节）", nameof(data));
            }

            bool isWrite = data != null;

            switch (profile.FrameType)
            {
                case McFrameType.A1E:
                    return BuildA1ERequest(profile, address, points, payload, isWrite);
                case McFrameType.Q3E:
                    return BuildQna3ERequest(profile, address, points, payload, isWrite);
                case McFrameType.Q4E:
                    return BuildQna4ERequest(profile, address, points, payload, isWrite, serialNumber);
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), profile.FrameType, "未知的 MC 帧类型");
            }
        }

        /// <summary>
        /// 1E 请求：副头部(1) | PC号(1) | 监视定时器(2) | 设备号(4) | 软元件码(2) | 点数(1) | 固定值(1)
        /// </summary>
        private static byte[] BuildA1ERequest(McProfile profile, McAddress address, int points, byte[] payload, bool isWrite)
        {
            bool isBit = address.IsBitDevice;

            byte subHeader;
            if (isWrite)
                subHeader = isBit ? (byte)0x02 : (byte)0x03;
            else
                subHeader = isBit ? (byte)0x00 : (byte)0x01;

            var frame = new byte[12 + payload.Length];
            frame[0] = subHeader;
            frame[1] = 0xFF;
            McCommonMethods.WriteUInt16(frame, 2, DefaultMonitorTimer);

            Array.Copy(address.GetDeviceNumberBytes(4), 0, frame, 4, 4);

            byte[] deviceCode = profile.GetDeviceCode(address.Device);
            Array.Copy(deviceCode, 0, frame, 8, 2);

            // 点数 1 字节，0 表示 256；其后 1 字节固定为 00
            frame[10] = points == 256 ? (byte)0x00 : (byte)points;
            frame[11] = 0x00;

            if (payload.Length > 0)
                Array.Copy(payload, 0, frame, 12, payload.Length);

            return frame;
        }

        /// <summary>
        /// 3E 请求：副头部(2) | 网络(1) | PC号(1) | IO号(2) | 站号(1) | 请求数据长度(2) |
        /// 监视定时器(2) | 指令(2) | 子指令(2) | 设备号(3|4) | 软元件码(1|2) | 点数(2) | 数据
        /// </summary>
        private static byte[] BuildQna3ERequest(McProfile profile, McAddress address, int points, byte[] payload, bool isWrite)
        {
            int addressWidth = profile.AddressWidth;
            int codeWidth = profile.DeviceCodeWidth;
            int headerLength = 9 + 2 + 2 + 2 + addressWidth + codeWidth + 2;

            var frame = new byte[headerLength + payload.Length];
            McCommonMethods.WriteUInt16(frame, 0, SubHeader3E);
            frame[2] = 0x00; // 网络号
            frame[3] = 0xFF; // PC 号
            frame[4] = 0xFF; // 目标模块 IO 号
            frame[5] = 0x03;
            frame[6] = 0x00; // 目标站号
            McCommonMethods.WriteUInt16(frame, 7, (ushort)(frame.Length - 9));

            McCommonMethods.WriteUInt16(frame, 9, DefaultMonitorTimer);
            McCommonMethods.WriteUInt16(frame, 11, isWrite ? CommandBatchWrite : CommandBatchRead);
            McCommonMethods.WriteUInt16(frame, 13, profile.GetSubCommand(address.IsBitDevice));

            WriteDeviceSection(frame, 15, profile, address, points, payload, addressWidth, codeWidth);
            return frame;
        }

        /// <summary>
        /// 4E 请求：副头部(2) | 序列号(2) | 保留(2) | 网络(1) | PC号(1) | IO号(2) | 站号(1) |
        /// 请求数据长度(2) | 监视定时器(2) | 指令(2) | 子指令(2) | 设备号 | 软元件码 | 点数 | 数据
        /// </summary>
        private static byte[] BuildQna4ERequest(McProfile profile, McAddress address, int points, byte[] payload,
            bool isWrite, ushort serialNumber)
        {
            int addressWidth = profile.AddressWidth;
            int codeWidth = profile.DeviceCodeWidth;
            int headerLength = 13 + 2 + 2 + 2 + addressWidth + codeWidth + 2;

            var frame = new byte[headerLength + payload.Length];
            McCommonMethods.WriteUInt16(frame, 0, SubHeader4E);
            McCommonMethods.WriteUInt16(frame, 2, serialNumber);
            McCommonMethods.WriteUInt16(frame, 4, 0x0000); // 保留
            frame[6] = 0x00; // 网络号
            frame[7] = 0xFF; // PC 号
            frame[8] = 0xFF; // 目标模块 IO 号
            frame[9] = 0x03;
            frame[10] = 0x00; // 目标站号
            McCommonMethods.WriteUInt16(frame, 11, (ushort)(frame.Length - 13));

            McCommonMethods.WriteUInt16(frame, 13, DefaultMonitorTimer);
            McCommonMethods.WriteUInt16(frame, 15, isWrite ? CommandBatchWrite : CommandBatchRead);
            McCommonMethods.WriteUInt16(frame, 17, profile.GetSubCommand(address.IsBitDevice));

            WriteDeviceSection(frame, 19, profile, address, points, payload, addressWidth, codeWidth);
            return frame;
        }

        private static void WriteDeviceSection(byte[] frame, int offset, McProfile profile, McAddress address,
            int points, byte[] payload, int addressWidth, int codeWidth)
        {
            Array.Copy(address.GetDeviceNumberBytes(addressWidth), 0, frame, offset, addressWidth);
            offset += addressWidth;

            byte[] deviceCode = profile.GetDeviceCode(address.Device);
            Array.Copy(deviceCode, 0, frame, offset, codeWidth);
            offset += codeWidth;

            McCommonMethods.WriteUInt16(frame, offset, (ushort)points);
            offset += 2;

            if (payload.Length > 0)
                Array.Copy(payload, 0, frame, offset, payload.Length);
        }

        /// <summary>
        /// 1E 响应：副头部(1) | 结束码(1) | [异常码(1)] | 数据
        /// </summary>
        private static McPacket ParseA1EResponse(byte[] frame)
        {
            if (frame.Length < 2)
                throw new ArgumentException($"A-1E 响应长度不足：实际 {frame.Length} 字节，至少需要 2 字节", nameof(frame));

            byte subHeader = frame[0];
            if ((subHeader & 0x80) == 0)
                throw new ArgumentException($"A-1E 响应副头部非法: 0x{subHeader:X2}", nameof(frame));

            byte endCode = frame[1];
            byte abnormalCode = 0;
            int dataOffset = 2;

            if (endCode == McCommonMethods.EndCodeA1EAbnormal)
            {
                if (frame.Length >= 3)
                    abnormalCode = frame[2];
                dataOffset = 3;
            }

            var packet = new McPacket
            {
                FrameType = McFrameType.A1E,
                SubHeader = subHeader,
                EndCode = endCode,
                AbnormalCode = abnormalCode
            };

            if (packet.IsSuccess)
            {
                packet.Data = ExtractData(frame, dataOffset);
            }
            else
            {
                packet.Data = Empty;
                packet.ErrorMessage = McCommonMethods.FormatA1EEndCode(endCode, abnormalCode);
            }

            return packet;
        }

        /// <summary>
        /// 3E / 4E 响应：副头部(2) | [序列号(2) | 保留(2)] | 网络(1) | PC号(1) | IO号(2) | 站号(1) |
        /// 响应数据长度(2，含结束码) | 结束码(2) | 数据
        /// </summary>
        private static McPacket ParseQnaResponse(byte[] frame, bool is4E, ushort? expectedSerialNumber = null)
        {
            int lengthFieldOffset = is4E ? 11 : 7;
            int endCodeOffset = lengthFieldOffset + 2;
            int dataOffset = endCodeOffset + 2;
            int headerLength = dataOffset;

            if (frame.Length < headerLength)
                throw new ArgumentException(
                    $"{(is4E ? "4E" : "3E")} 响应长度不足：实际 {frame.Length} 字节，至少需要 {headerLength} 字节", nameof(frame));

            ushort expectedSubHeader = is4E ? SubHeader4EResponse : SubHeader3EResponse;
            ushort actualSubHeader = McCommonMethods.ReadUInt16(frame, 0);
            if (actualSubHeader != expectedSubHeader)
                throw new ArgumentException(
                    $"{(is4E ? "4E" : "3E")} 响应副头部非法: 0x{actualSubHeader:X4}，期望 0x{expectedSubHeader:X4}", nameof(frame));

            var packet = new McPacket
            {
                FrameType = is4E ? McFrameType.Q4E : McFrameType.Q3E,
                SubHeader = frame[0]
            };

            int routeOffset = 2;
            if (is4E)
            {
                packet.SerialNumber = McCommonMethods.ReadUInt16(frame, 2);
                if (expectedSerialNumber.HasValue && packet.SerialNumber != expectedSerialNumber.Value)
                    throw new InvalidOperationException(
                        $"4E 序列号不匹配：期望 0x{expectedSerialNumber.Value:X4}，实际 0x{packet.SerialNumber:X4}，判定为帧错位");

                routeOffset = 6; // 跳过序列号与保留字段
            }

            packet.NetworkNumber = frame[routeOffset];
            packet.PcNumber = frame[routeOffset + 1];
            packet.ModuleIoNumber = McCommonMethods.ReadUInt16(frame, routeOffset + 2);
            packet.StationNumber = frame[routeOffset + 4];

            ushort dataLength = McCommonMethods.ReadUInt16(frame, lengthFieldOffset);
            if (dataLength < 2)
                throw new ArgumentException(
                    $"{(is4E ? "4E" : "3E")} 响应数据长度 0x{dataLength:X4} 小于结束码长度 2", nameof(frame));

            int expectedTotal = endCodeOffset + dataLength;
            if (frame.Length != expectedTotal)
                throw new ArgumentException(
                    $"{(is4E ? "4E" : "3E")} 响应长度字段 0x{dataLength:X4} 对应总长 {expectedTotal} 字节，实际 {frame.Length} 字节", nameof(frame));

            packet.EndCode = McCommonMethods.ReadUInt16(frame, endCodeOffset);
            if (packet.IsSuccess)
            {
                packet.Data = ExtractData(frame, dataOffset);
            }
            else
            {
                packet.Data = Empty;
                packet.ErrorMessage = McCommonMethods.FormatEndCode(packet.EndCode);
            }

            return packet;
        }

        /// <summary>单帧最大点数（按手册每通信最大点数）</summary>
        public static int GetMaxPoints(McFrameType frameType, bool isIqR, bool isBit)
        {
            if (frameType == McFrameType.A1E)
                return isBit ? 256 : 128;

            if (isIqR)
                return isBit ? 7904 : 960;

            return isBit ? 7168 : 960;
        }

        /// <summary>
        /// 按首字节嗅探帧类型（服务端用）：00~03 → 1E，50 → 3E，54 → 4E。
        /// </summary>
        public static McFrameType SniffFrameType(byte firstByte)
        {
            switch (firstByte)
            {
                case 0x50: return McFrameType.Q3E;
                case 0x54: return McFrameType.Q4E;
            }

            if (firstByte <= 0x03)
                return McFrameType.A1E;

            throw new ArgumentException($"无法识别的 MC 帧首字节: 0x{firstByte:X2}", nameof(firstByte));
        }

        /// <summary>
        /// 推算完整请求帧长度所需的最小字节数：1E 为 12（点数字段之后才开始数据区），
        /// 3E 为 9（含请求数据长度字段），4E 为 13。
        /// </summary>
        public static int GetRequestHeaderLength(McFrameType frameType)
        {
            switch (frameType)
            {
                case McFrameType.A1E: return 12;
                case McFrameType.Q3E: return 9;
                case McFrameType.Q4E: return 13;
                default: throw new ArgumentOutOfRangeException(nameof(frameType), frameType, "未知的 MC 帧类型");
            }
        }

        /// <summary>
        /// 从已缓冲的字节流推算一帧请求的总长度。字节不够时返回 false，调用方继续等数据。
        /// 3E/4E 的长度字段可信度由 <see cref="ParseRequest"/> 再校验一次。
        /// </summary>
        public static bool TryGetRequestLength(McFrameType frameType, byte[] buffer, out int totalLength)
        {
            totalLength = 0;
            if (buffer == null)
                return false;

            switch (frameType)
            {
                case McFrameType.A1E:
                    if (buffer.Length < 12)
                        return false;

                    totalLength = 12 + GetA1EWriteDataLength(buffer[0], GetA1EPoints(buffer));
                    return true;

                case McFrameType.Q3E:
                    if (buffer.Length < 9)
                        return false;

                    totalLength = 9 + McCommonMethods.ReadUInt16(buffer, 7);
                    return true;

                case McFrameType.Q4E:
                    if (buffer.Length < 13)
                        return false;

                    totalLength = 13 + McCommonMethods.ReadUInt16(buffer, 11);
                    return true;

                default:
                    throw new ArgumentOutOfRangeException(nameof(frameType), frameType, "未知的 MC 帧类型");
            }
        }

        /// <summary>
        /// 解析请求帧（服务端用），与 <see cref="BuildReadRequest"/> / <see cref="BuildWriteRequest"/> 互逆。
        /// 帧结构非法时抛出 <see cref="ArgumentException"/>；软元件码无法识别时 <see cref="McPacket.Device"/> 为 null。
        /// </summary>
        public static McPacket ParseRequest(McFrameType frameType, byte[] frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));

            switch (frameType)
            {
                case McFrameType.A1E:
                    return ParseA1ERequest(frame);
                case McFrameType.Q3E:
                case McFrameType.Q4E:
                    return ParseQnaRequest(frame, frameType == McFrameType.Q4E);
                default:
                    throw new ArgumentOutOfRangeException(nameof(frameType), frameType, "未知的 MC 帧类型");
            }
        }

        /// <summary>
        /// 构造响应帧。结束码非正常时不带数据（1E 回 5B + 异常码）。
        /// </summary>
        /// <param name="request">对应的请求包，用于回显路由与序列号</param>
        /// <param name="endCode">结束码，0 表示正常</param>
        /// <param name="data">数据区（位：每点 4 bit；字：每点 2 字节小端）</param>
        public static byte[] BuildResponse(McPacket request, ushort endCode, byte[] data)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            byte[] payload = data ?? Empty;

            switch (request.FrameType)
            {
                case McFrameType.A1E:
                    return BuildA1EResponse(request, endCode, payload);
                case McFrameType.Q3E:
                    return BuildQnaResponse(request, false, endCode, payload);
                case McFrameType.Q4E:
                    return BuildQnaResponse(request, true, endCode, payload);
                default:
                    throw new ArgumentOutOfRangeException(nameof(request), request.FrameType, "未知的 MC 帧类型");
            }
        }

        /// <summary>构造错误响应（不带数据区）</summary>
        public static byte[] BuildErrorResponse(McPacket request, ushort endCode)
        {
            return BuildResponse(request, endCode, Empty);
        }

        private static McPacket ParseA1ERequest(byte[] frame)
        {
            if (frame.Length < 12)
                throw new ArgumentException($"A-1E 请求长度不足：实际 {frame.Length} 字节，至少需要 12 字节", nameof(frame));

            bool isWrite = frame[0] >= 0x02 && frame[0] <= 0x03;
            bool isBit = (frame[0] & 0x01) == 0;
            int points = GetA1EPoints(frame);
            int dataLength = GetA1EWriteDataLength(frame[0], points);
            int expectedLength = 12 + dataLength;
            if (frame.Length != expectedLength)
                throw new ArgumentException(
                    $"A-1E 请求长度与点数不符：点数 {points} 应为 {expectedLength} 字节，实际 {frame.Length} 字节", nameof(frame));

            var packet = new McPacket
            {
                FrameType = McFrameType.A1E,
                SubHeader = frame[0],
                Command = isWrite ? CommandBatchWrite : CommandBatchRead,
                IsBit = isBit,
                PcNumber = frame[1],
                Points = points,
                StartAddress = ReadDeviceNumber(frame, 4, 4),
                Device = McDeviceDescriptor.FindByCode(McFrameType.A1E, false, McCommonMethods.ReadUInt16(frame, 8)),
                Data = dataLength > 0 ? ExtractData(frame, 12) : Empty
            };

            return packet;
        }

        private static McPacket ParseQnaRequest(byte[] frame, bool is4E)
        {
            int lengthFieldOffset = is4E ? 11 : 7;
            int minimumHeader = lengthFieldOffset + 2;
            if (frame.Length < minimumHeader)
                throw new ArgumentException(
                    $"{(is4E ? "4E" : "3E")} 请求长度不足：实际 {frame.Length} 字节，至少需要 {minimumHeader} 字节", nameof(frame));

            ushort requestDataLength = McCommonMethods.ReadUInt16(frame, lengthFieldOffset);
            int expectedLength = minimumHeader + requestDataLength;
            if (frame.Length != expectedLength)
                throw new ArgumentException(
                    $"{(is4E ? "4E" : "3E")} 请求长度字段 0x{requestDataLength:X4} 对应总长 {expectedLength} 字节，实际 {frame.Length} 字节", nameof(frame));

            int routeOffset = is4E ? 6 : 2;
            int subCommandOffset = is4E ? 17 : 13;

            if (frame.Length < subCommandOffset + 2)
                throw new ArgumentException($"{(is4E ? "4E" : "3E")} 请求缺少子指令字段", nameof(frame));

            ushort subCommand = McCommonMethods.ReadUInt16(frame, subCommandOffset);
            bool isIqR = subCommand >= 0x0002;
            bool isBit = (subCommand & 0x0001) == 0x0001;

            int deviceOffset = is4E ? 19 : 15;
            int addressWidth = isIqR ? 4 : 3;
            int codeWidth = isIqR ? 2 : 1;
            int codeOffset = deviceOffset + addressWidth;
            int pointsOffset = codeOffset + codeWidth;
            int dataOffset = pointsOffset + 2;

            if (frame.Length < dataOffset)
                throw new ArgumentException($"{(is4E ? "4E" : "3E")} 请求缺少软元件或点数字段", nameof(frame));

            ushort command = McCommonMethods.ReadUInt16(frame, subCommandOffset - 2);
            int points = McCommonMethods.ReadUInt16(frame, pointsOffset);

            // 数据区长度是否与点数匹配由服务端判定（回 C058），这里只保证分帧可信：
            // 帧长已与声明长度字段对齐，数据区就是剩余字节
            ushort deviceCode = codeWidth == 1 ? frame[codeOffset] : McCommonMethods.ReadUInt16(frame, codeOffset);
            McFrameType packetType = is4E ? McFrameType.Q4E : McFrameType.Q3E;

            var packet = new McPacket
            {
                FrameType = packetType,
                SubHeader = frame[0],
                SerialNumber = is4E ? McCommonMethods.ReadUInt16(frame, 2) : (ushort)0,
                NetworkNumber = frame[routeOffset],
                PcNumber = frame[routeOffset + 1],
                ModuleIoNumber = McCommonMethods.ReadUInt16(frame, routeOffset + 2),
                StationNumber = frame[routeOffset + 4],
                Command = command,
                SubCommand = subCommand,
                IsBit = isBit,
                IsIqR = isIqR,
                Points = points,
                StartAddress = ReadDeviceNumber(frame, deviceOffset, addressWidth),
                Device = McDeviceDescriptor.FindByCode(packetType, isIqR, deviceCode),
                Data = ExtractData(frame, dataOffset)
            };

            return packet;
        }

        private static byte[] BuildA1EResponse(McPacket request, ushort endCode, byte[] data)
        {
            byte subHeader = (byte)(request.SubHeader | 0x80);
            if (endCode != McCommonMethods.EndCodeNormal)
            {
                return new[]
                {
                    subHeader,
                    McCommonMethods.EndCodeA1EAbnormal,
                    McCommonMethods.MapA1EAbnormalCode(endCode)
                };
            }

            var frame = new byte[2 + data.Length];
            frame[0] = subHeader;
            frame[1] = 0x00;
            if (data.Length > 0)
                Array.Copy(data, 0, frame, 2, data.Length);
            return frame;
        }

        private static byte[] BuildQnaResponse(McPacket request, bool is4E, ushort endCode, byte[] data)
        {
            int dataOffset = is4E ? 15 : 11;
            int lengthFieldOffset = is4E ? 11 : 7;
            int endCodeOffset = is4E ? 13 : 9;
            int routeOffset = is4E ? 6 : 2;

            var frame = new byte[dataOffset + data.Length];
            McCommonMethods.WriteUInt16(frame, 0, is4E ? SubHeader4EResponse : SubHeader3EResponse);
            if (is4E)
            {
                McCommonMethods.WriteUInt16(frame, 2, request.SerialNumber);
                McCommonMethods.WriteUInt16(frame, 4, 0x0000);
            }

            frame[routeOffset] = request.NetworkNumber;
            frame[routeOffset + 1] = request.PcNumber;
            McCommonMethods.WriteUInt16(frame, routeOffset + 2, request.ModuleIoNumber);
            frame[routeOffset + 4] = request.StationNumber;

            McCommonMethods.WriteUInt16(frame, lengthFieldOffset, (ushort)(data.Length + 2));
            McCommonMethods.WriteUInt16(frame, endCodeOffset, endCode);

            if (data.Length > 0)
                Array.Copy(data, 0, frame, dataOffset, data.Length);

            return frame;
        }

        private static int ReadDeviceNumber(byte[] frame, int offset, int width)
        {
            int value = 0;
            for (int i = 0; i < width; i++)
            {
                value |= frame[offset + i] << (8 * i);
            }

            return value;
        }

        private static int GetA1EPoints(byte[] frame)
        {
            int points = frame[10];
            return points == 0 ? 256 : points;
        }

        private static int GetA1EWriteDataLength(byte subHeader, int points)
        {
            if (subHeader < 0x02 || subHeader > 0x03)
                return 0;

            bool isBit = (subHeader & 0x01) == 0;
            return isBit ? (points + 1) / 2 : points * 2;
        }

        private static byte[] ExtractData(byte[] frame, int offset)
        {
            if (frame.Length <= offset)
                return Empty;

            var data = new byte[frame.Length - offset];
            Array.Copy(frame, offset, data, 0, data.Length);
            return data;
        }
    }
}
