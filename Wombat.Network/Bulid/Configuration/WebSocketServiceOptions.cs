using System;
using System.Collections.Generic;
using System.Net;
using Wombat.Network.WebSockets;

namespace Wombat.Network.Build.Configuration
{
    /// <summary>
    /// WebSocket服务配置选项
    /// </summary>
    public class WebSocketServiceOptions
    {
        /// <summary>
        /// 服务器配置
        /// </summary>
        public WebSocketServerOptions Server { get; set; } = new WebSocketServerOptions();

        /// <summary>
        /// 客户端配置
        /// </summary>
        public WebSocketClientOptions Client { get; set; } = new WebSocketClientOptions();
    }

    /// <summary>
    /// WebSocket服务器配置选项
    /// </summary>
    public class WebSocketServerOptions
    {
        /// <summary>
        /// 监听地址，默认为任意地址
        /// </summary>
        public string ListenAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// 监听端口
        /// </summary>
        public int ListenPort { get; set; } = 8082;

        /// <summary>
        /// 是否自动启动服务器
        /// </summary>
        public bool AutoStart { get; set; } = true;

        /// <summary>
        /// 服务器配置
        /// </summary>
        public WebSocketServerConfiguration Configuration { get; set; }

        /// <summary>
        /// 模块目录配置
        /// </summary>
        public AsyncWebSocketServerModuleCatalog ModuleCatalog { get; set; }

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
    /// WebSocket客户端配置选项
    /// </summary>
    public class WebSocketClientOptions
    {
        /// <summary>
        /// 服务器URI
        /// </summary>
        public string ServerUri { get; set; } = "ws://127.0.0.1:8082";

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
        public WebSocketClientConfiguration Configuration { get; set; }

        /// <summary>
        /// 获取服务器URI
        /// </summary>
        public Uri GetServerUri()
        {
            return new Uri(ServerUri);
        }
    }
} 