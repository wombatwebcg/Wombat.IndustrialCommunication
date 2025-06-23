using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Wombat.Network;

namespace Wombat.Network.Sockets
{
    public sealed class TcpSocketServerConfiguration
    {
        public TcpSocketServerConfiguration()
            : this(new SegmentBufferManager(1024, 8192, 1, true))
        {
        }

        public TcpSocketServerConfiguration(ISegmentBufferManager bufferManager)
        {
            BufferManager = bufferManager;

            ReceiveBufferSize = 8192;
            SendBufferSize = 8192;
            ReceiveTimeout = TimeSpan.Zero;
            SendTimeout = TimeSpan.Zero;
            NoDelay = true;
            LingerState = new LingerOption(false, 0);
            KeepAlive = false;
            KeepAliveInterval = TimeSpan.FromSeconds(5);
            ReuseAddress = false;

            PendingConnectionBacklog = 200;
            AllowNatTraversal = true;

            SslEnabled = false;
            SslServerCertificate = null;
            SslEncryptionPolicy = EncryptionPolicy.RequireEncryption;
            SslEnabledProtocols = SslProtocols.Ssl3 | SslProtocols.Tls;
            SslClientCertificateRequired = true;
            SslCheckCertificateRevocation = false;
            SslPolicyErrorsBypassed = false;

            ConnectTimeout = TimeSpan.FromSeconds(15);
            FrameBuilder = new LengthPrefixedFrameBuilder();
            
            // 心跳相关配置
            EnableHeartbeat = false;                   // 默认禁用应用层心跳
            HeartbeatInterval = TimeSpan.FromSeconds(30); // 心跳发送间隔
            HeartbeatTimeout = TimeSpan.FromSeconds(90); // 心跳接收超时时间
            MaxMissedHeartbeats = 3;                   // 最大允许缺失心跳次数
        }

        public ISegmentBufferManager BufferManager { get; set; }

        public int ReceiveBufferSize { get; set; }
        public int SendBufferSize { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public bool NoDelay { get; set; }
        public LingerOption LingerState { get; set; }
        public bool KeepAlive { get; set; }
        public TimeSpan KeepAliveInterval { get; set; }
        public bool ReuseAddress { get; set; }

        public int PendingConnectionBacklog { get; set; }
        public bool AllowNatTraversal { get; set; }

        public bool SslEnabled { get; set; }
        public X509Certificate2 SslServerCertificate { get; set; }
        public EncryptionPolicy SslEncryptionPolicy { get; set; }
        public SslProtocols SslEnabledProtocols { get; set; }
        public bool SslClientCertificateRequired { get; set; }
        public bool SslCheckCertificateRevocation { get; set; }
        public bool SslPolicyErrorsBypassed { get; set; }

        public TimeSpan ConnectTimeout { get; set; }
        public IFrameBuilder FrameBuilder { get; set; }
        
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
