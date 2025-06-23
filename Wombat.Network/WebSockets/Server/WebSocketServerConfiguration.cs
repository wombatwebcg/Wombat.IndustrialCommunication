using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Wombat.Network.WebSockets.Extensions;
using Wombat.Network.WebSockets.SubProtocols;

namespace Wombat.Network.WebSockets
{
    /// <summary>
    /// WebSocket 服务器配置类，包含所有用于配置 WebSocket 服务器行为的设置。
    /// </summary>
    /// <remarks>
    /// 此类提供了全面的配置选项，包括缓冲区管理、网络设置、SSL/TLS 安全配置、
    /// 连接超时设置以及 WebSocket 扩展和子协议支持。
    /// 所有配置项都有合理的默认值，可以根据具体需求进行调整。
    /// </remarks>
    public sealed class WebSocketServerConfiguration
    {
        /// <summary>
        /// 初始化 <see cref="WebSocketServerConfiguration"/> 类的新实例，并设置默认配置值。
        /// </summary>
        public WebSocketServerConfiguration()
        {
            BufferManager = new SegmentBufferManager(1024, 8192, 1, true);
            ReceiveBufferSize = 8192;
            SendBufferSize = 8192;
            ReceiveTimeout = TimeSpan.Zero;
            SendTimeout = TimeSpan.Zero;
            NoDelay = true;
            LingerState = new LingerOption(false, 0); // The socket will linger for x seconds after Socket.Close is called.

            PendingConnectionBacklog = 200;
            AllowNatTraversal = true;

            SslEnabled = false;
            SslServerCertificate = null;
            SslEncryptionPolicy = EncryptionPolicy.RequireEncryption;
            SslEnabledProtocols = SslProtocols.Ssl3 | SslProtocols.Tls;
            SslClientCertificateRequired = true;
            SslCheckCertificateRevocation = false;
            SslPolicyErrorsBypassed = false;

            ConnectTimeout = TimeSpan.FromSeconds(10);
            CloseTimeout = TimeSpan.FromSeconds(5);
            KeepAliveInterval = TimeSpan.FromSeconds(60);
            KeepAliveTimeout = TimeSpan.FromSeconds(15);
            ReasonableFragmentSize = 4096;

            //EnabledExtensions = new Dictionary<string, IWebSocketExtensionNegotiator>()
            //{
            //    { PerMessageCompressionExtension.RegisteredToken, new PerMessageCompressionExtensionNegotiator() },
            //};

            EnabledExtensions = new Dictionary<string, IWebSocketExtensionNegotiator>();
            EnabledSubProtocols = new Dictionary<string, IWebSocketSubProtocolNegotiator>();
        }

        /// <summary>
        /// 获取或设置缓冲区管理器，用于高效管理网络通信中的内存缓冲区。
        /// </summary>
        /// <value>默认为具有1024个8KB缓冲区的 <see cref="SegmentBufferManager"/> 实例。</value>
        public ISegmentBufferManager BufferManager { get; set; }

        /// <summary>
        /// 获取或设置接收缓冲区的大小（以字节为单位）。
        /// </summary>
        /// <value>默认值为 8192 字节（8KB）。</value>
        public int ReceiveBufferSize { get; set; }

        /// <summary>
        /// 获取或设置发送缓冲区的大小（以字节为单位）。
        /// </summary>
        /// <value>默认值为 8192 字节（8KB）。</value>
        public int SendBufferSize { get; set; }

        /// <summary>
        /// 获取或设置接收操作的超时时间。
        /// </summary>
        /// <value>默认值为 <see cref="TimeSpan.Zero"/>，表示无超时限制。</value>
        public TimeSpan ReceiveTimeout { get; set; }

        /// <summary>
        /// 获取或设置发送操作的超时时间。
        /// </summary>
        /// <value>默认值为 <see cref="TimeSpan.Zero"/>，表示无超时限制。</value>
        public TimeSpan SendTimeout { get; set; }

        /// <summary>
        /// 获取或设置是否禁用 Nagle 算法以减少网络延迟。
        /// </summary>
        /// <value>默认值为 <c>true</c>，表示禁用 Nagle 算法。</value>
        /// <remarks>
        /// 禁用 Nagle 算法可以减少小数据包的发送延迟，但可能增加网络流量。
        /// 对于实时通信应用，通常建议禁用 Nagle 算法。
        /// </remarks>
        public bool NoDelay { get; set; }

        /// <summary>
        /// 获取或设置套接字的逗留选项，控制套接字关闭时的行为。
        /// </summary>
        /// <value>默认为不逗留（<c>Enabled = false</c>，<c>LingerTime = 0</c>）。</value>
        /// <remarks>
        /// 逗留选项决定了当套接字关闭时，如果还有未发送的数据，套接字是否等待数据发送完成。
        /// </remarks>
        public LingerOption LingerState { get; set; }

        /// <summary>
        /// 获取或设置挂起连接队列的最大长度。
        /// </summary>
        /// <value>默认值为 200。</value>
        /// <remarks>
        /// 此值决定了在服务器繁忙时可以排队等待接受的连接数量。
        /// 超过此数量的连接请求将被拒绝。
        /// </remarks>
        public int PendingConnectionBacklog { get; set; }

        /// <summary>
        /// 获取或设置是否允许 NAT 穿越。
        /// </summary>
        /// <value>默认值为 <c>true</c>。</value>
        /// <remarks>
        /// 启用 NAT 穿越可以帮助客户端通过网络地址转换（NAT）设备连接到服务器。
        /// </remarks>
        public bool AllowNatTraversal { get; set; }

        /// <summary>
        /// 获取或设置是否启用 SSL/TLS 加密。
        /// </summary>
        /// <value>默认值为 <c>false</c>。</value>
        public bool SslEnabled { get; set; }

        /// <summary>
        /// 获取或设置 SSL 服务器证书。
        /// </summary>
        /// <value>默认值为 <c>null</c>。当 <see cref="SslEnabled"/> 为 <c>true</c> 时必须设置此属性。</value>
        public X509Certificate2 SslServerCertificate { get; set; }

        /// <summary>
        /// 获取或设置 SSL 加密策略。
        /// </summary>
        /// <value>默认值为 <see cref="EncryptionPolicy.RequireEncryption"/>。</value>
        public EncryptionPolicy SslEncryptionPolicy { get; set; }

        /// <summary>
        /// 获取或设置启用的 SSL/TLS 协议版本。
        /// </summary>
        /// <value>默认值为 <see cref="SslProtocols.Ssl3"/> | <see cref="SslProtocols.Tls"/>。</value>
        /// <remarks>
        /// 建议使用更安全的 TLS 版本，避免使用已知存在安全漏洞的协议版本。
        /// </remarks>
        public SslProtocols SslEnabledProtocols { get; set; }

        /// <summary>
        /// 获取或设置是否要求客户端提供证书。
        /// </summary>
        /// <value>默认值为 <c>true</c>。</value>
        public bool SslClientCertificateRequired { get; set; }

        /// <summary>
        /// 获取或设置是否检查证书吊销状态。
        /// </summary>
        /// <value>默认值为 <c>false</c>。</value>
        /// <remarks>
        /// 启用证书吊销检查可以提高安全性，但可能影响连接建立的性能。
        /// </remarks>
        public bool SslCheckCertificateRevocation { get; set; }

        /// <summary>
        /// 获取或设置是否绕过 SSL 策略错误。
        /// </summary>
        /// <value>默认值为 <c>false</c>。</value>
        /// <remarks>
        /// 仅在开发和测试环境中使用，生产环境不建议绕过 SSL 策略错误。
        /// </remarks>
        public bool SslPolicyErrorsBypassed { get; set; }

        /// <summary>
        /// 获取或设置连接建立的超时时间。
        /// </summary>
        /// <value>默认值为 10 秒。</value>
        public TimeSpan ConnectTimeout { get; set; }

        /// <summary>
        /// 获取或设置连接关闭的超时时间。
        /// </summary>
        /// <value>默认值为 5 秒。</value>
        /// <remarks>
        /// 此超时时间用于等待优雅关闭握手完成的最大时间。
        /// </remarks>
        public TimeSpan CloseTimeout { get; set; }

        /// <summary>
        /// 获取或设置保活心跳的发送间隔。
        /// </summary>
        /// <value>默认值为 60 秒。</value>
        /// <remarks>
        /// 保活心跳用于检测连接是否仍然有效，防止空闲连接被中间设备断开。
        /// </remarks>
        public TimeSpan KeepAliveInterval { get; set; }

        /// <summary>
        /// 获取或设置等待保活心跳响应的超时时间。
        /// </summary>
        /// <value>默认值为 15 秒。</value>
        /// <remarks>
        /// 如果在此时间内未收到心跳响应，连接将被视为已断开。
        /// </remarks>
        public TimeSpan KeepAliveTimeout { get; set; }

        /// <summary>
        /// 获取或设置合理的消息分片大小（以字节为单位）。
        /// </summary>
        /// <value>默认值为 4096 字节（4KB）。</value>
        /// <remarks>
        /// 大消息将被分割成不超过此大小的片段进行传输，以避免阻塞其他消息的传输。
        /// </remarks>
        public int ReasonableFragmentSize { get; set; }

        /// <summary>
        /// 获取或设置启用的 WebSocket 扩展协商器字典。
        /// </summary>
        /// <value>键为扩展名称，值为对应的扩展协商器实例。默认为空字典。</value>
        /// <remarks>
        /// WebSocket 扩展可以提供额外功能，如消息压缩、多路复用等。
        /// 扩展的协商在 WebSocket 握手过程中进行。
        /// </remarks>
        public Dictionary<string, IWebSocketExtensionNegotiator> EnabledExtensions { get; set; }

        /// <summary>
        /// 获取或设置启用的 WebSocket 子协议协商器字典。
        /// </summary>
        /// <value>键为子协议名称，值为对应的子协议协商器实例。默认为空字典。</value>
        /// <remarks>
        /// WebSocket 子协议定义了在 WebSocket 连接上使用的应用层协议。
        /// 子协议的协商在 WebSocket 握手过程中进行。
        /// </remarks>
        public Dictionary<string, IWebSocketSubProtocolNegotiator> EnabledSubProtocols { get; set; }
    }
}
