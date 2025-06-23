using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wombat.Network.Build.Builders;
using Wombat.Network.Build.Configuration;
using Wombat.Network.Build.HostedServices;
using Wombat.Network.Sockets;

namespace Wombat.Network.Build.Extensions
{
    /// <summary>
    /// TCP Socket服务扩展方法
    /// </summary>
    public static class TcpSocketServiceExtensions
    {
        /// <summary>
        /// 添加TCP服务器
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configureServer">服务器配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddTcpServer(
            this IServiceCollection services,
            Action<ITcpSocketServerBuilder> configureServer = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // 注册配置选项
            services.Configure<TcpSocketServiceOptions>(options => { });

            // 创建构建器并配置
            var builder = new TcpSocketServerBuilder(services);
            configureServer?.Invoke(builder);

            // 如果没有注册事件分发器，使用默认的
            if (!services.HasService<ITcpSocketServerEventDispatcher>())
            {
                services.AddSingleton<ITcpSocketServerEventDispatcher>(provider =>
                    new DefaultTcpSocketServerEventDispatcher());
            }

            // 注册托管服务
            services.AddHostedService<TcpSocketServerHostedService>();

            return services;
        }

        /// <summary>
        /// 添加TCP服务器（使用简单的事件处理器）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="listenPort">监听端口</param>
        /// <param name="onSessionDataReceived">会话数据接收事件</param>
        /// <param name="onSessionStarted">会话开始事件</param>
        /// <param name="onSessionClosed">会话关闭事件</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddTcpServer(
            this IServiceCollection services,
            int listenPort,
            Func<TcpSocketSession, byte[], int, int, System.Threading.Tasks.Task> onSessionDataReceived = null,
            Func<TcpSocketSession, System.Threading.Tasks.Task> onSessionStarted = null,
            Func<TcpSocketSession, System.Threading.Tasks.Task> onSessionClosed = null)
        {
            return services.AddTcpServer(builder =>
            {
                builder.Listen(listenPort)
                       .UseHandlers(onSessionDataReceived, onSessionStarted, onSessionClosed);
            });
        }

        /// <summary>
        /// 添加TCP客户端
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configureClient">客户端配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddTcpClient(
            this IServiceCollection services,
            Action<TcpClientOptions> configureClient = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // 注册配置选项
            services.Configure<TcpSocketServiceOptions>(options =>
            {
                configureClient?.Invoke(options.Client);
            });

            // 注册TCP客户端工厂
            services.AddSingleton<ITcpSocketClientFactory, TcpSocketClientFactory>();

            return services;
        }

        /// <summary>
        /// 添加TCP客户端（使用简单配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="remoteAddress">远程服务器地址</param>
        /// <param name="remotePort">远程服务器端口</param>
        /// <param name="onServerDataReceived">服务器数据接收事件</param>
        /// <param name="onServerConnected">服务器连接事件</param>
        /// <param name="onServerDisconnected">服务器断开连接事件</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddTcpClient(
            this IServiceCollection services,
            string remoteAddress,
            int remotePort,
            Func<TcpSocketClient, byte[], int, int, System.Threading.Tasks.Task> onServerDataReceived = null,
            Func<TcpSocketClient, System.Threading.Tasks.Task> onServerConnected = null,
            Func<TcpSocketClient, System.Threading.Tasks.Task> onServerDisconnected = null)
        {
            return services.AddTcpClient(options =>
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
    /// TCP客户端工厂接口
    /// </summary>
    public interface ITcpSocketClientFactory
    {
        /// <summary>
        /// 创建TCP客户端
        /// </summary>
        /// <param name="dispatcher">事件分发器</param>
        /// <returns>TCP客户端</returns>
        TcpSocketClient CreateClient(ITcpSocketClientEventDispatcher dispatcher);

        /// <summary>
        /// 创建TCP客户端（使用简单的事件处理器）
        /// </summary>
        /// <param name="onServerDataReceived">服务器数据接收事件</param>
        /// <param name="onServerConnected">服务器连接事件</param>
        /// <param name="onServerDisconnected">服务器断开连接事件</param>
        /// <returns>TCP客户端</returns>
        TcpSocketClient CreateClient(
            Func<TcpSocketClient, byte[], int, int, System.Threading.Tasks.Task> onServerDataReceived = null,
            Func<TcpSocketClient, System.Threading.Tasks.Task> onServerConnected = null,
            Func<TcpSocketClient, System.Threading.Tasks.Task> onServerDisconnected = null);
    }

    /// <summary>
    /// TCP客户端工厂实现
    /// </summary>
    internal class TcpSocketClientFactory : ITcpSocketClientFactory
    {
        private readonly TcpSocketServiceOptions _options;

        public TcpSocketClientFactory(IOptions<TcpSocketServiceOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public TcpSocketClient CreateClient(ITcpSocketClientEventDispatcher dispatcher)
        {
            var remoteEndPoint = _options.Client.GetRemoteEndPoint();
            var localEndPoint = _options.Client.GetLocalEndPoint();
            var configuration = _options.Client.Configuration ?? new TcpSocketClientConfiguration();

            return localEndPoint != null
                ? new TcpSocketClient(remoteEndPoint, localEndPoint, dispatcher, configuration)
                : new TcpSocketClient(remoteEndPoint, dispatcher, configuration);
        }

        public TcpSocketClient CreateClient(
            Func<TcpSocketClient, byte[], int, int, System.Threading.Tasks.Task> onServerDataReceived = null,
            Func<TcpSocketClient, System.Threading.Tasks.Task> onServerConnected = null,
            Func<TcpSocketClient, System.Threading.Tasks.Task> onServerDisconnected = null)
        {
            var dispatcher = new DefaultTcpSocketClientEventDispatcher(
                onServerDataReceived, onServerConnected, onServerDisconnected);
            return CreateClient(dispatcher);
        }
    }
} 