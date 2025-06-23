using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wombat.Network.Build.Configuration;
using Wombat.Network.Sockets;

namespace Wombat.Network.Build.HostedServices
{
    /// <summary>
    /// TCP服务器托管服务
    /// </summary>
    public class TcpSocketServerHostedService : BackgroundService
    {
        private readonly ILogger<TcpSocketServerHostedService> _logger;
        private readonly TcpSocketServiceOptions _options;
        private readonly ITcpSocketServerEventDispatcher _dispatcher;
        private TcpSocketServer _server;

        public TcpSocketServerHostedService(
            ILogger<TcpSocketServerHostedService> logger,
            IOptions<TcpSocketServiceOptions> options,
            ITcpSocketServerEventDispatcher dispatcher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>
        /// 获取TCP服务器实例
        /// </summary>
        public TcpSocketServer Server => _server;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var endPoint = _options.Server.GetListenEndPoint();
                var configuration = _options.Server.Configuration ?? new TcpSocketServerConfiguration();

                _server = new TcpSocketServer(endPoint, _dispatcher, configuration);
                _server.UseLogger(_logger);

                if (_options.Server.AutoStart)
                {
                    _logger.LogInformation("正在启动TCP服务器，监听地址: {EndPoint}", endPoint);
                    _server.Listen();
                    _logger.LogInformation("TCP服务器已启动，监听地址: {EndPoint}", endPoint);

                    // 等待停止信号
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                else
                {
                    _logger.LogInformation("TCP服务器已创建但未自动启动，监听地址: {EndPoint}", endPoint);
                    // 等待停止信号
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常关闭，不记录错误
                _logger.LogInformation("TCP服务器正在关闭");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TCP服务器运行时发生错误");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("正在停止TCP服务器");
            
            try
            {
                if (_server != null && _server.IsListening)
                {
                    _server.Shutdown();
                    _logger.LogInformation("TCP服务器已停止");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止TCP服务器时发生错误");
            }

            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            try
            {
                _server?.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "释放TCP服务器资源时发生错误");
            }
            _server = null;
            base.Dispose();
        }
    }
} 