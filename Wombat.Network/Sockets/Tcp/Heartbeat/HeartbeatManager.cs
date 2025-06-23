using System;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Wombat.Network.Sockets
{
    /// <summary>
    /// 心跳管理器，用于处理TCP连接的心跳机制
    /// </summary>
    public class HeartbeatManager
    {
        // 特殊心跳包魔术字节序列，使用不太可能出现在正常数据中的字节组合
        // 0xFE 0xFD 0xFC 0xFB + "WBHT" (Wombat Heartbeat)标识
        private static readonly byte[] HeartbeatMagic = new byte[] { 0xFE, 0xFD, 0xFC, 0xFB, 
                                                                      (byte)'W', (byte)'B', (byte)'H', (byte)'T' };
        
        // 心跳包完整格式：
        // 8字节魔术头(0xFEFDFCFB + "WBHT") + 8字节时间戳 + 2字节校验和
        private const int HeartbeatPacketLength = 18;
        
        /// <summary>
        /// 检查给定的数据是否是心跳包
        /// </summary>
        /// <param name="data">待检查的数据</param>
        /// <param name="offset">数据起始位置</param>
        /// <param name="count">数据长度</param>
        /// <returns>是否为心跳包</returns>
        public static bool IsHeartbeatPacket(byte[] data, int offset, int count)
        {
            if (data == null || offset < 0 || count < HeartbeatPacketLength)
                return false;
            
            // 首先检查魔术头是否匹配
            for (int i = 0; i < HeartbeatMagic.Length; i++)
            {
                if (data[offset + i] != HeartbeatMagic[i])
                    return false;
            }
            
            // 验证校验和
            ushort checksum = BitConverter.ToUInt16(data, offset + HeartbeatPacketLength - 2);
            ushort calculatedChecksum = CalculateChecksum(data, offset, HeartbeatPacketLength - 2);
            
            return checksum == calculatedChecksum;
        }
        
        /// <summary>
        /// 创建一个心跳包
        /// </summary>
        /// <returns>心跳包字节数组</returns>
        public static byte[] CreateHeartbeatPacket()
        {
            byte[] packet = new byte[HeartbeatPacketLength];
            
            // 复制魔术头
            Buffer.BlockCopy(HeartbeatMagic, 0, packet, 0, HeartbeatMagic.Length);
            
            // 添加当前时间戳
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            byte[] timeBytes = BitConverter.GetBytes(timestamp);
            Buffer.BlockCopy(timeBytes, 0, packet, HeartbeatMagic.Length, 8);
            
            // 计算并添加校验和
            ushort checksum = CalculateChecksum(packet, 0, HeartbeatPacketLength - 2);
            byte[] checksumBytes = BitConverter.GetBytes(checksum);
            Buffer.BlockCopy(checksumBytes, 0, packet, HeartbeatPacketLength - 2, 2);
            
            return packet;
        }
        
        /// <summary>
        /// 从心跳包中提取时间戳
        /// </summary>
        /// <param name="data">心跳包数据</param>
        /// <param name="offset">数据起始位置</param>
        /// <returns>时间戳</returns>
        public static long ExtractTimestamp(byte[] data, int offset)
        {
            if (data == null || offset < 0 || data.Length - offset < HeartbeatPacketLength)
                return 0;
                
            byte[] timeBytes = new byte[8];
            Buffer.BlockCopy(data, offset + HeartbeatMagic.Length, timeBytes, 0, 8);
            return BitConverter.ToInt64(timeBytes, 0);
        }
        
        /// <summary>
        /// 计算数据的校验和
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="offset">起始位置</param>
        /// <param name="length">长度</param>
        /// <returns>校验和</returns>
        private static ushort CalculateChecksum(byte[] data, int offset, int length)
        {
            uint sum = 0;
            int i;
            
            // 简单的校验和算法
            for (i = 0; i < length; i++)
            {
                sum += data[offset + i];
            }
            
            // 折叠32位sum到16位
            while (sum >> 16 != 0)
            {
                sum = (sum & 0xFFFF) + (sum >> 16);
            }
            
            return (ushort)~sum; // 返回校验和的反码
        }
        
        /// <summary>
        /// 记录心跳日志
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="message">日志消息</param>
        /// <param name="args">日志参数</param>
        public static void LogHeartbeat(ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            
            logger.LogDebug($"[♥ HEARTBEAT ♥] {message}", args);
        }
    }
} 