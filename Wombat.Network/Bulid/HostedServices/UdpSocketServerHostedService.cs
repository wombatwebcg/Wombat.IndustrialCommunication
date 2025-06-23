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
    /// UDP服务器托管服务
    /// </summary>
    public class UdpSocketServerHostedService : BackgroundService
    {
        private readonly ILogger<UdpSocketServerHostedService> _logger;
        private readonly UdpSocketServiceOptions _options;
        private readonly IUdpSocketServerEventDispatcher _dispatcher;
        private UdpSocketServer _server;

        public UdpSocketServerHostedService(
            ILogger<UdpSocketServerHostedService> logger,
            IOptions<UdpSocketServiceOptions> options,
            IUdpSocketServerEventDispatcher dispatcher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>
        /// 获取UDP服务器实例
        /// </summary>
        public UdpSocketServer Server => _server;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var endPoint = _options.Server.GetListenEndPoint();
                var configuration = _options.Server.Configuration ?? new UdpSocketServerConfiguration();

                _server = new UdpSocketServer(endPoint, _dispatcher, configuration);
                _server.UseLogger(_logger);

                if (_options.Server.AutoStart)
                {
                    _logger.LogInformation("正在启动UDP服务器，监听地址: {EndPoint}", endPoint);
                    await _server.Listen(stoppingToken);
                    _logger.LogInformation("UDP服务器已启动，监听地址: {EndPoint}", endPoint);
                }
                else
                {
                    _logger.LogInformation("UDP服务器已创建但未自动启动，监听地址: {EndPoint}", endPoint);
                    // 等待停止信号
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常关闭，不记录错误
                _logger.LogInformation("UDP服务器正在关闭");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UDP服务器运行时发生错误");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("正在停止UDP服务器");
            
            try
            {
                if (_server != null)
                {
                    await _server.Close();
                    _logger.LogInformation("UDP服务器已停止");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止UDP服务器时发生错误");
            }

            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            try
            {
                _server?.Close().Wait(5000); // 等待最多5秒
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "释放UDP服务器资源时发生错误");
            }
            _server = null;
            base.Dispose();
        }
    }
} 