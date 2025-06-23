using System;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wombat.Network.Build.Configuration;
using Wombat.Network.Build.HostedServices;
using Wombat.Network.Sockets;

namespace Wombat.Network.Build.Extensions
{
    /// <summary>
    /// UDP Socket服务扩展方法
    /// </summary>
    public static class UdpSocketServiceExtensions
    {
        /// <summary>
        /// 添加UDP服务器
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="listenPort">监听端口</param>
        /// <param name="configureServer">服务器配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddUdpServer(
            this IServiceCollection services,
            int listenPort,
            Action<UdpServerOptions> configureServer = null)
        {
            return services.AddUdpServer(options =>
            {
                options.ListenPort = listenPort;
                configureServer?.Invoke(options);
            });
        }

        /// <summary>
        /// 添加UDP服务器
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configureServer">服务器配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddUdpServer(
            this IServiceCollection services,
            Action<UdpServerOptions> configureServer = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // 注册配置选项
            services.Configure<UdpSocketServiceOptions>(options =>
            {
                configureServer?.Invoke(options.Server);
            });

            // 如果没有注册事件分发器，使用默认的
            if (!services.HasService<IUdpSocketServerEventDispatcher>())
            {
                services.AddSingleton<IUdpSocketServerEventDispatcher>(provider =>
                    new DefaultUdpSocketServerEventDispatcher());
            }

            // 注册托管服务
            services.AddHostedService<UdpSocketServerHostedService>();

            return services;
        }

        /// <summary>
        /// 添加UDP服务器（使用简单的事件处理器）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="listenPort">监听端口</param>
        /// <param name="onSessionDataReceived">会话数据接收事件</param>
        /// <param name="onSessionStarted">会话开始事件</param>
        /// <param name="onSessionClosed">会话关闭事件</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddUdpServer(
            this IServiceCollection services,
            int listenPort,
            Func<UdpSocketSession, byte[], int, int, System.Threading.Tasks.Task> onSessionDataReceived = null,
            Func<UdpSocketSession, System.Threading.Tasks.Task> onSessionStarted = null,
            Func<UdpSocketSession, System.Threading.Tasks.Task> onSessionClosed = null)
        {
            // 注册事件分发器
            var dispatcher = new DefaultUdpSocketServerEventDispatcher(
                onSessionDataReceived, onSessionStarted, onSessionClosed);
            services.AddSingleton<IUdpSocketServerEventDispatcher>(dispatcher);

            return services.AddUdpServer(options =>
            {
                options.ListenPort = listenPort;
            });
        }

        /// <summary>
        /// 添加UDP客户端
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configureClient">客户端配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddUdpClient(
            this IServiceCollection services,
            Action<UdpClientOptions> configureClient = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // 注册配置选项
            services.Configure<UdpSocketServiceOptions>(options =>
            {
                configureClient?.Invoke(options.Client);
            });

            // 注册UDP客户端工厂
            services.AddSingleton<IUdpSocketClientFactory, UdpSocketClientFactory>();

            return services;
        }

        /// <summary>
        /// 添加UDP客户端（使用简单配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="remoteAddress">远程服务器地址</param>
        /// <param name="remotePort">远程服务器端口</param>
        /// <param name="onServerDataReceived">服务器数据接收事件</param>
        /// <param name="onServerConnected">服务器连接事件</param>
        /// <param name="onServerDisconnected">服务器断开连接事件</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddUdpClient(
            this IServiceCollection services,
            string remoteAddress,
            int remotePort,
            Func<UdpSocketClient, byte[], int, int, IPEndPoint, System.Threading.Tasks.Task> onServerDataReceived = null,
            Func<UdpSocketClient, System.Threading.Tasks.Task> onServerConnected = null,
            Func<UdpSocketClient, System.Threading.Tasks.Task> onServerDisconnected = null)
        {
            return services.AddUdpClient(options =>
            {
                options.RemoteAddress = remoteAddress;
                options.RemotePort = remotePort;
            });
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
    /// UDP客户端工厂接口
    /// </summary>
    public interface IUdpSocketClientFactory
    {
        /// <summary>
        /// 创建UDP客户端
        /// </summary>
        /// <param name="dispatcher">事件分发器</param>
        /// <returns>UDP客户端</returns>
        UdpSocketClient CreateClient(IUdpSocketClientEventDispatcher dispatcher);

        /// <summary>
        /// 创建UDP客户端（使用简单的事件处理器）
        /// </summary>
        /// <param name="onServerDataReceived">服务器数据接收事件</param>
        /// <param name="onServerConnected">服务器连接事件</param>
        /// <param name="onServerDisconnected">服务器断开连接事件</param>
        /// <returns>UDP客户端</returns>
        UdpSocketClient CreateClient(
            Func<UdpSocketClient, byte[], int, int, IPEndPoint, System.Threading.Tasks.Task> onServerDataReceived = null,
            Func<UdpSocketClient, System.Threading.Tasks.Task> onServerConnected = null,
            Func<UdpSocketClient, System.Threading.Tasks.Task> onServerDisconnected = null);
    }

    /// <summary>
    /// UDP客户端工厂实现
    /// </summary>
    internal class UdpSocketClientFactory : IUdpSocketClientFactory
    {
        private readonly UdpSocketServiceOptions _options;

        public UdpSocketClientFactory(IOptions<UdpSocketServiceOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public UdpSocketClient CreateClient(IUdpSocketClientEventDispatcher dispatcher)
        {
            var remoteEndPoint = _options.Client.GetRemoteEndPoint();
            var localEndPoint = _options.Client.GetLocalEndPoint();
            var configuration = _options.Client.Configuration ?? new UdpSocketClientConfiguration();

            return localEndPoint != null
                ? new UdpSocketClient(remoteEndPoint, localEndPoint, dispatcher, configuration)
                : new UdpSocketClient(remoteEndPoint, dispatcher, configuration);
        }

        public UdpSocketClient CreateClient(
            Func<UdpSocketClient, byte[], int, int, IPEndPoint, System.Threading.Tasks.Task> onServerDataReceived = null,
            Func<UdpSocketClient, System.Threading.Tasks.Task> onServerConnected = null,
            Func<UdpSocketClient, System.Threading.Tasks.Task> onServerDisconnected = null)
        {
            var dispatcher = new DefaultUdpSocketClientEventDispatcher(
                onServerDataReceived, onServerConnected, onServerDisconnected);
            return CreateClient(dispatcher);
        }
    }
} 