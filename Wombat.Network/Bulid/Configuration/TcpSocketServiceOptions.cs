using System;
using System.Net;
using Wombat.Network.Sockets;

namespace Wombat.Network.Build.Configuration
{
    /// <summary>
    /// TCP服务配置选项
    /// </summary>
    public class TcpSocketServiceOptions
    {
        /// <summary>
        /// 服务器配置
        /// </summary>
        public TcpServerOptions Server { get; set; } = new TcpServerOptions();

        /// <summary>
        /// 客户端配置
        /// </summary>
        public TcpClientOptions Client { get; set; } = new TcpClientOptions();
    }

    /// <summary>
    /// TCP服务器配置选项
    /// </summary>
    public class TcpServerOptions
    {
        /// <summary>
        /// 监听地址，默认为任意地址
        /// </summary>
        public string ListenAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// 监听端口
        /// </summary>
        public int ListenPort { get; set; } = 8080;

        /// <summary>
        /// 是否自动启动服务器
        /// </summary>
        public bool AutoStart { get; set; } = true;

        /// <summary>
        /// 服务器配置
        /// </summary>
        public TcpSocketServerConfiguration Configuration { get; set; }

        /// <summary>
        /// 获取监听终结点
        /// </summary>
        public IPEndPoint GetListenEndPoint()
        {
            if (IPAddress.TryParse(ListenAddress, out var address))
            {
                return new IPEndPoint(address, ListenPort);
            }
            return new IPEndPoint(IPAddress.Any, ListenPort);
        }
    }

    /// <summary>
    /// TCP客户端配置选项
    /// </summary>
    public class TcpClientOptions
    {
        /// <summary>
        /// 远程服务器地址
        /// </summary>
        public string RemoteAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// 远程服务器端口
        /// </summary>
        public int RemotePort { get; set; } = 8080;

        /// <summary>
        /// 本地绑定地址（可选）
        /// </summary>
        public string LocalAddress { get; set; }

        /// <summary>
        /// 本地绑定端口（可选，0表示自动分配）
        /// </summary>
        public int LocalPort { get; set; } = 0;

        /// <summary>
        /// 是否自动连接
        /// </summary>
        public bool AutoConnect { get; set; } = false;

        /// <summary>
        /// 是否启用自动重连
        /// </summary>
        public bool EnableAutoReconnect { get; set; } = true;

        /// <summary>
        /// 重连间隔时间
        /// </summary>
        public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 最大重连次数（0表示无限重连）
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 0;

        /// <summary>
        /// 客户端配置
        /// </summary>
        public TcpSocketClientConfiguration Configuration { get; set; }

        /// <summary>
        /// 获取远程终结点
        /// </summary>
        public IPEndPoint GetRemoteEndPoint()
        {
            if (IPAddress.TryParse(RemoteAddress, out var address))
            {
                return new IPEndPoint(address, RemotePort);
            }
            
            // 尝试解析主机名
            try
            {
                var addresses = Dns.GetHostAddresses(RemoteAddress);
                if (addresses.Length > 0)
                {
                    return new IPEndPoint(addresses[0], RemotePort);
                }
            }
            catch
            {
                // 解析失败，使用默认值
            }
            
            return new IPEndPoint(IPAddress.Loopback, RemotePort);
        }

        /// <summary>
        /// 获取本地终结点（如果配置了的话）
        /// </summary>
        public IPEndPoint GetLocalEndPoint()
        {
            if (string.IsNullOrEmpty(LocalAddress))
                return null;
                
            if (IPAddress.TryParse(LocalAddress, out var address))
            {
                return new IPEndPoint(address, LocalPort);
            }
            
            return null;
        }
    }
} 