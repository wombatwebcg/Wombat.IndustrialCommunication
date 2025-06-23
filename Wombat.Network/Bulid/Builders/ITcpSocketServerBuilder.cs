using System;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Wombat.Network.Sockets;

namespace Wombat.Network.Build.Builders
{
    /// <summary>
    /// TCP服务器构建器接口
    /// </summary>
    public interface ITcpSocketServerBuilder
    {
        /// <summary>
        /// 配置监听地址和端口
        /// </summary>
        /// <param name="address">监听地址</param>
        /// <param name="port">监听端口</param>
        ITcpSocketServerBuilder Listen(string address, int port);

        /// <summary>
        /// 配置监听端口（使用默认地址0.0.0.0）
        /// </summary>
        /// <param name="port">监听端口</param>
        ITcpSocketServerBuilder Listen(int port);

        /// <summary>
        /// 配置监听终结点
        /// </summary>
        /// <param name="endPoint">监听终结点</param>
        ITcpSocketServerBuilder Listen(IPEndPoint endPoint);

        /// <summary>
        /// 配置TCP服务器选项
        /// </summary>
        /// <param name="configureOptions">配置委托</param>
        ITcpSocketServerBuilder Configure(Action<TcpSocketServerConfiguration> configureOptions);

        /// <summary>
        /// 设置是否自动启动
        /// </summary>
        /// <param name="autoStart">是否自动启动</param>
        ITcpSocketServerBuilder AutoStart(bool autoStart = true);

        /// <summary>
        /// 使用事件分发器
        /// </summary>
        /// <param name="dispatcher">事件分发器</param>
        ITcpSocketServerBuilder UseDispatcher(ITcpSocketServerEventDispatcher dispatcher);

        /// <summary>
        /// 使用事件分发器
        /// </summary>
        /// <typeparam name="TDispatcher">事件分发器类型</typeparam>
        ITcpSocketServerBuilder UseDispatcher<TDispatcher>() where TDispatcher : class, ITcpSocketServerEventDispatcher;

        /// <summary>
        /// 使用简单的事件处理器
        /// </summary>
        /// <param name="onSessionDataReceived">会话数据接收事件</param>
        /// <param name="onSessionStarted">会话开始事件</param>
        /// <param name="onSessionClosed">会话关闭事件</param>
        ITcpSocketServerBuilder UseHandlers(
            Func<TcpSocketSession, byte[], int, int, System.Threading.Tasks.Task> onSessionDataReceived = null,
            Func<TcpSocketSession, System.Threading.Tasks.Task> onSessionStarted = null,
            Func<TcpSocketSession, System.Threading.Tasks.Task> onSessionClosed = null);

        /// <summary>
        /// 获取服务集合
        /// </summary>
        IServiceCollection Services { get; }
    }
} 