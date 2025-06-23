using System;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wombat.Network.Build.Configuration;
using Wombat.Network.Sockets;

namespace Wombat.Network.Build.Builders
{
    /// <summary>
    /// TCP服务器构建器实现
    /// </summary>
    public class TcpSocketServerBuilder : ITcpSocketServerBuilder
    {
        private readonly IServiceCollection _services;

        public TcpSocketServerBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IServiceCollection Services => _services;

        public ITcpSocketServerBuilder Listen(string address, int port)
        {
            _services.Configure<TcpSocketServiceOptions>(options =>
            {
                options.Server.ListenAddress = address;
                options.Server.ListenPort = port;
            });
            return this;
        }

        public ITcpSocketServerBuilder Listen(int port)
        {
            return Listen("0.0.0.0", port);
        }

        public ITcpSocketServerBuilder Listen(IPEndPoint endPoint)
        {
            return Listen(endPoint.Address.ToString(), endPoint.Port);
        }

        public ITcpSocketServerBuilder Configure(Action<TcpSocketServerConfiguration> configureOptions)
        {
            if (configureOptions == null)
                throw new ArgumentNullException(nameof(configureOptions));

            _services.Configure<TcpSocketServiceOptions>(options =>
            {
                if (options.Server.Configuration == null)
                    options.Server.Configuration = new TcpSocketServerConfiguration();
                configureOptions(options.Server.Configuration);
            });
            return this;
        }

        public ITcpSocketServerBuilder AutoStart(bool autoStart = true)
        {
            _services.Configure<TcpSocketServiceOptions>(options =>
            {
                options.Server.AutoStart = autoStart;
            });
            return this;
        }

        public ITcpSocketServerBuilder UseDispatcher(ITcpSocketServerEventDispatcher dispatcher)
        {
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            _services.AddSingleton(dispatcher);
            return this;
        }

        public ITcpSocketServerBuilder UseDispatcher<TDispatcher>() where TDispatcher : class, ITcpSocketServerEventDispatcher
        {
            _services.AddSingleton<ITcpSocketServerEventDispatcher, TDispatcher>();
            return this;
        }

        public ITcpSocketServerBuilder UseHandlers(
            Func<TcpSocketSession, byte[], int, int, System.Threading.Tasks.Task> onSessionDataReceived = null,
            Func<TcpSocketSession, System.Threading.Tasks.Task> onSessionStarted = null,
            Func<TcpSocketSession, System.Threading.Tasks.Task> onSessionClosed = null)
        {
            var dispatcher = new DefaultTcpSocketServerEventDispatcher(
                onSessionDataReceived, onSessionStarted, onSessionClosed);
            return UseDispatcher(dispatcher);
        }
    }
} 