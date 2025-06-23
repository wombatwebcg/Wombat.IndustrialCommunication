using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Wombat.Network;

namespace Wombat.Network.Sockets
{
    public sealed class TcpSocketClientConfiguration
    {
        public TcpSocketClientConfiguration()
            : this(new SegmentBufferManager(100, 8192, 1, true))
        {
        }

        public TcpSocketClientConfiguration(ISegmentBufferManager bufferManager)
        {
            BufferManager = bufferManager;

            ReceiveBufferSize = 8192;                   // Specifies the total per-socket buffer space reserved for receives. This is unrelated to the maximum message size or the size of a TCP window.
            SendBufferSize = 8192;                      // Specifies the total per-socket buffer space reserved for sends. This is unrelated to the maximum message size or the size of a TCP window.
            ReceiveTimeout = TimeSpan.FromSeconds(30);  // Receive a time-out. This option applies only to synchronous methods; it has no effect on asynchronous methods such as the BeginSend method.
            SendTimeout = TimeSpan.FromSeconds(30);     // Send a time-out. This option applies only to synchronous methods; it has no effect on asynchronous methods such as the BeginSend method.
            NoDelay = true;                             // Disables the Nagle algorithm for send coalescing.
            LingerState = new LingerOption(false, 0);   // The socket will linger for x seconds after Socket.Close is called.
            KeepAlive = false;                          // Use keep-alives.
            KeepAliveInterval = TimeSpan.FromSeconds(5);// https://msdn.microsoft.com/en-us/library/system.net.sockets.socketoptionname(v=vs.110).aspx
            ReuseAddress = false;                       // Allows the socket to be bound to an address that is already in use.

            SslEnabled = false;
            SslTargetHost = null;
            SslClientCertificates = new X509CertificateCollection();
            SslEncryptionPolicy = EncryptionPolicy.RequireEncryption;
            SslEnabledProtocols = SslProtocols.Ssl3 | SslProtocols.Tls;
            SslCheckCertificateRevocation = false;
            SslPolicyErrorsBypassed = false;
            ConnectTimeout = TimeSpan.FromSeconds(30);  // Increased from 2 to 30 seconds
           
            FrameBuilder = new LengthPrefixedFrameBuilder();
            
            // 新增属性
            OperationTimeout = TimeSpan.FromSeconds(30);
            EnablePipelineIo = true;
            MaxConcurrentConnections = 100;
            MaxConcurrentOperations = 10;
            MaxReceiveBufferSize = 10240 * 1024; // 默认为10MB
            
            // 心跳相关配置
            EnableHeartbeat = false;                   // 默认禁用应用层心跳
            HeartbeatInterval = TimeSpan.FromSeconds(30); // 心跳发送间隔
            HeartbeatTimeout = TimeSpan.FromSeconds(90); // 心跳接收超时时间
            MaxMissedHeartbeats = 3;                   // 最大允许缺失心跳次数
        }

        public ISegmentBufferManager BufferManager { get; set; }

        public int ReceiveBufferSize { get; set; }
        public int SendBufferSize { get; set; }
        public bool NoDelay { get; set; }
        public LingerOption LingerState { get; set; }
        public bool KeepAlive { get; set; }
        public TimeSpan KeepAliveInterval { get; set; }
        public bool ReuseAddress { get; set; }

        public bool SslEnabled { get; set; }
        public string SslTargetHost { get; set; }
        public X509CertificateCollection SslClientCertificates { get; set; }
        public EncryptionPolicy SslEncryptionPolicy { get; set; }
        public SslProtocols SslEnabledProtocols { get; set; }
        public bool SslCheckCertificateRevocation { get; set; }
        public bool SslPolicyErrorsBypassed { get; set; }

        public TimeSpan ConnectTimeout { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }

        public IFrameBuilder FrameBuilder { get; set; }
        
        // 新增属性
        /// <summary>
        /// 用于设置一般操作的超时时间，如处理接收到的消息等
        /// </summary>
        public TimeSpan OperationTimeout { get; set; }
        
        /// <summary>
        /// 是否启用System.IO.Pipelines进行高性能I/O
        /// </summary>
        public bool EnablePipelineIo { get; set; }
        
        /// <summary>
        /// 最大并发连接数
        /// </summary>
        public int MaxConcurrentConnections { get; set; }
        
        /// <summary>
        /// 每个连接的最大并发操作数
        /// </summary>
        public int MaxConcurrentOperations { get; set; }
        
        /// <summary>
        /// 接收数据缓冲区的最大大小（字节数）
        /// 当数据量超过此值时，新数据将不再加入缓冲区
        /// </summary>
        public int MaxReceiveBufferSize { get; set; }
        
        /// <summary>
        /// 是否启用应用层心跳包
        /// </summary>
        public bool EnableHeartbeat { get; set; }
        
        /// <summary>
        /// 心跳包发送间隔
        /// </summary>
        public TimeSpan HeartbeatInterval { get; set; }
        
        /// <summary>
        /// 心跳包超时时间，超过此时间未收到心跳则认为连接断开
        /// </summary>
        public TimeSpan HeartbeatTimeout { get; set; }
        
        /// <summary>
        /// 最大允许缺失心跳次数，超过此次数未收到心跳则认为连接断开
        /// </summary>
        public int MaxMissedHeartbeats { get; set; }
    }
}
