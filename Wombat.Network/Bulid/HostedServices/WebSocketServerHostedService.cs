using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wombat.Network.Build.Configuration;
using Wombat.Network.WebSockets;

namespace Wombat.Network.Build.HostedServices
{
    /// <summary>
    /// WebSocket服务器托管服务
    /// </summary>
    public class WebSocketServerHostedService : BackgroundService
    {
        private readonly ILogger<WebSocketServerHostedService> _logger;
        private readonly WebSocketServiceOptions _options;
        private readonly AsyncWebSocketServerModuleCatalog _moduleCatalog;
        private WebSocketServer _server;

        public WebSocketServerHostedService(
            ILogger<WebSocketServerHostedService> logger,
            IOptions<WebSocketServiceOptions> options,
            AsyncWebSocketServerModuleCatalog moduleCatalog)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _moduleCatalog = moduleCatalog ?? throw new ArgumentNullException(nameof(moduleCatalog));
        }

        /// <summary>
        /// 获取WebSocket服务器实例
        /// </summary>
        public WebSocketServer Server => _server;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var endPoint = _options.Server.GetListenEndPoint();
                var configuration = _options.Server.Configuration ?? new WebSocketServerConfiguration();
                var catalog = _options.Server.ModuleCatalog ?? _moduleCatalog;

                _server = new WebSocketServer(endPoint, catalog, configuration);
                _server.UsgLogger(_logger);

                if (_options.Server.AutoStart)
                {
                    _logger.LogInformation("正在启动WebSocket服务器，监听地址: {EndPoint}", endPoint);
                    _server.Listen();
                    _logger.LogInformation("WebSocket服务器已启动，监听地址: {EndPoint}", endPoint);

                    // 等待停止信号
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                else
                {
                    _logger.LogInformation("WebSocket服务器已创建但未自动启动，监听地址: {EndPoint}", endPoint);
                    // 等待停止信号
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常关闭，不记录错误
                _logger.LogInformation("WebSocket服务器正在关闭");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket服务器运行时发生错误");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("正在停止WebSocket服务器");
            
            try
            {
                if (_server != null && _server.IsListening)
                {
                    _server.Shutdown();
                    _logger.LogInformation("WebSocket服务器已停止");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止WebSocket服务器时发生错误");
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
                _logger.LogError(ex, "释放WebSocket服务器资源时发生错误");
            }
            _server = null;
            base.Dispose();
        }
    }
} 