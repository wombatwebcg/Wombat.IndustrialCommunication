using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wombat.Network.Build.Configuration;
using Wombat.Network.Build.HostedServices;
using Wombat.Network.WebSockets;

namespace Wombat.Network.Build.Extensions
{
    /// <summary>
    /// WebSocket服务扩展方法
    /// </summary>
    public static class WebSocketServiceExtensions
    {
        /// <summary>
        /// 添加WebSocket服务器
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="listenPort">监听端口</param>
        /// <param name="configureServer">服务器配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddWebSocketServer(
            this IServiceCollection services,
            int listenPort,
            Action<WebSocketServerOptions> configureServer = null)
        {
            return services.AddWebSocketServer(options =>
            {
                options.ListenPort = listenPort;
                configureServer?.Invoke(options);
            });
        }

        /// <summary>
        /// 添加WebSocket服务器
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configureServer">服务器配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddWebSocketServer(
            this IServiceCollection services,
            Action<WebSocketServerOptions> configureServer = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // 注册配置选项
            services.Configure<WebSocketServiceOptions>(options =>
            {
                configureServer?.Invoke(options.Server);
            });

            // 如果没有注册模块目录，使用默认的
            if (!services.HasService<AsyncWebSocketServerModuleCatalog>())
            {
                services.AddSingleton<AsyncWebSocketServerModuleCatalog>(provider =>
                    new AsyncWebSocketServerModuleCatalog());
            }

            // 注册托管服务
            services.AddHostedService<WebSocketServerHostedService>();

            return services;
        }

        /// <summary>
        /// 添加WebSocket客户端
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configureClient">客户端配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddWebSocketClient(
            this IServiceCollection services,
            Action<WebSocketClientOptions> configureClient = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // 注册配置选项
            services.Configure<WebSocketServiceOptions>(options =>
            {
                configureClient?.Invoke(options.Client);
            });

            // 注册WebSocket客户端工厂
            services.AddSingleton<IWebSocketClientFactory, WebSocketClientFactory>();

            return services;
        }

        /// <summary>
        /// 添加WebSocket客户端（使用简单配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="serverUri">服务器URI</param>
        /// <param name="onServerTextReceived">服务器文本消息接收事件</param>
        /// <param name="onServerBinaryReceived">服务器二进制消息接收事件</param>
        /// <param name="onServerConnected">服务器连接事件</param>
        /// <param name="onServerDisconnected">服务器断开连接事件</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddWebSocketClient(
            this IServiceCollection services,
            string serverUri,
            Func<WebSocketClient, string, System.Threading.Tasks.Task> onServerTextReceived = null,
            Func<WebSocketClient, byte[], int, int, System.Threading.Tasks.Task> onServerBinaryReceived = null,
            Func<WebSocketClient, System.Threading.Tasks.Task> onServerConnected = null,
            Func<WebSocketClient, System.Threading.Tasks.Task> onServerDisconnected = null)
        {
            return services.AddWebSocketClient(options =>
            {
                options.ServerUri = serverUri;
            });
        }

        /// <summary>
        /// 使用WebSocket模块
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configureModules">模块配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection UseWebSocketModules(
            this IServiceCollection services,
            Action<AsyncWebSocketServerModuleCatalog> configureModules = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<AsyncWebSocketServerModuleCatalog>(provider =>
            {
                var catalog = new AsyncWebSocketServerModuleCatalog();
                configureModules?.Invoke(catalog);
                return catalog;
            });

            return services;
        }

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        private static bool HasService<T>(this IServiceCollection services)
        {
            foreach (var service in services)
            {
                if (service.ServiceType == typeof(T))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// WebSocket客户端工厂接口
    /// </summary>
    public interface IWebSocketClientFactory
    {
        /// <summary>
        /// 创建WebSocket客户端
        /// </summary>
        /// <param name="dispatcher">消息分发器</param>
        /// <returns>WebSocket客户端</returns>
        WebSocketClient CreateClient(IWebSocketClientMessageDispatcher dispatcher);

        /// <summary>
        /// 创建WebSocket客户端（使用简单的事件处理器）
        /// </summary>
        /// <param name="onServerTextReceived">服务器文本消息接收事件</param>
        /// <param name="onServerBinaryReceived">服务器二进制消息接收事件</param>
        /// <param name="onServerConnected">服务器连接事件</param>
        /// <param name="onServerDisconnected">服务器断开连接事件</param>
        /// <returns>WebSocket客户端</returns>
        WebSocketClient CreateClient(
            Func<WebSocketClient, string, System.Threading.Tasks.Task> onServerTextReceived = null,
            Func<WebSocketClient, byte[], int, int, System.Threading.Tasks.Task> onServerBinaryReceived = null,
            Func<WebSocketClient, System.Threading.Tasks.Task> onServerConnected = null,
            Func<WebSocketClient, System.Threading.Tasks.Task> onServerDisconnected = null);
    }

    /// <summary>
    /// WebSocket客户端工厂实现
    /// </summary>
    internal class WebSocketClientFactory : IWebSocketClientFactory
    {
        private readonly WebSocketServiceOptions _options;

        public WebSocketClientFactory(IOptions<WebSocketServiceOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public WebSocketClient CreateClient(IWebSocketClientMessageDispatcher dispatcher)
        {
            var serverUri = _options.Client.GetServerUri();
            var configuration = _options.Client.Configuration ?? new WebSocketClientConfiguration();

            return new WebSocketClient(serverUri, dispatcher, configuration);
        }

        public WebSocketClient CreateClient(
            Func<WebSocketClient, string, System.Threading.Tasks.Task> onServerTextReceived = null,
            Func<WebSocketClient, byte[], int, int, System.Threading.Tasks.Task> onServerBinaryReceived = null,
            Func<WebSocketClient, System.Threading.Tasks.Task> onServerConnected = null,
            Func<WebSocketClient, System.Threading.Tasks.Task> onServerDisconnected = null)
        {
            var serverUri = _options.Client.GetServerUri();
            var configuration = _options.Client.Configuration ?? new WebSocketClientConfiguration();

            return new WebSocketClient(serverUri, onServerTextReceived, onServerBinaryReceived, 
                onServerConnected, onServerDisconnected, configuration);
        }
    }
} 