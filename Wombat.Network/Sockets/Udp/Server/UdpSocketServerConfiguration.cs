using System;
using System.Net.Sockets;
using Wombat.Network;

namespace Wombat.Network.Sockets
{
    public sealed class UdpSocketServerConfiguration
    {
        public UdpSocketServerConfiguration()
            : this(new SegmentBufferManager(1024, 8192, 1, true))
        {
        }

        public UdpSocketServerConfiguration(ISegmentBufferManager bufferManager)
        {
            BufferManager = bufferManager;

            ReceiveBufferSize = 8192;                   // Specifies the total per-socket buffer space reserved for receives.
            SendBufferSize = 8192;                      // Specifies the total per-socket buffer space reserved for sends.
            ReceiveTimeout = TimeSpan.FromSeconds(30);  // Receive a time-out.
            SendTimeout = TimeSpan.FromSeconds(30);     // Send a time-out.
            ReuseAddress = false;                       // Allows the socket to be bound to an address that is already in use.
            DontFragment = false;                       // Set the Don't Fragment flag in IP header.
            Broadcast = false;                          // Allow sending broadcast packets.
            
            OperationTimeout = TimeSpan.FromSeconds(30); // General operation timeout
            MaxReceiveBufferSize = 10240 * 1024;        // 默认为10MB
            
            FrameBuilder = new LengthPrefixedFrameBuilder(); // For datagram framing/parsing
            
            // 客户端管理相关配置
            ClientTimeout = TimeSpan.FromMinutes(10);    // 客户端无活动超时时间
            MaxClients = 1000;                          // 最大并发客户端数量
            CleanupInterval = TimeSpan.FromMinutes(1);   // 清理非活跃客户端的间隔
            
            // 心跳相关配置
            EnableHeartbeat = false;                   // 默认禁用应用层心跳
            HeartbeatInterval = TimeSpan.FromSeconds(30); // 心跳发送间隔
            HeartbeatTimeout = TimeSpan.FromSeconds(90); // 心跳接收超时时间
            MaxMissedHeartbeats = 3;                   // 最大允许缺失心跳次数
        }

        public ISegmentBufferManager BufferManager { get; set; }

        public int ReceiveBufferSize { get; set; }
        public int SendBufferSize { get; set; }
        public bool ReuseAddress { get; set; }
        public bool DontFragment { get; set; }
        public bool Broadcast { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public TimeSpan OperationTimeout { get; set; }

        public int MaxReceiveBufferSize { get; set; }
        public IFrameBuilder FrameBuilder { get; set; }
        
        // 客户端管理相关属性
        public TimeSpan ClientTimeout { get; set; }
        public int MaxClients { get; set; }
        public TimeSpan CleanupInterval { get; set; }
        
        // 心跳相关属性
        public bool EnableHeartbeat { get; set; }
        public TimeSpan HeartbeatInterval { get; set; }
        public TimeSpan HeartbeatTimeout { get; set; }
        public int MaxMissedHeartbeats { get; set; }
    }
} 