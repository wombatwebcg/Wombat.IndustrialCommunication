using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace Wombat.IndustrialCommunication.Gateway
{
    /// <summary>
    /// 网关扩展方法
    /// </summary>
    public static class GatewayExtensions
    {
        /// <summary>
        /// 添加工业通信网关服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddIndustrialGateway(this IServiceCollection services)
        {
            // 注册设备工厂
            services.AddSingleton<IDeviceFactory, DeviceFactory>();
            
            // 注册网关设备工厂
            services.AddSingleton<IGatewayDeviceFactory>(provider => 
            {
                var deviceFactory = provider.GetRequiredService<IDeviceFactory>();
                var logger = provider.GetService<ILogger<GatewayDeviceFactory>>();
                return new GatewayDeviceFactory(deviceFactory, logger);
            });
            
            return services;
        }
        
        /// <summary>
        /// 创建默认的网关设备工厂实例
        /// </summary>
        /// <returns>网关设备工厂实例</returns>
        public static IGatewayDeviceFactory CreateGatewayDeviceFactory()
        {
            var deviceFactory = new DeviceFactory();
            return new GatewayDeviceFactory(deviceFactory);
        }
        
        /// <summary>
        /// 创建使用指定日志记录器的网关设备工厂实例
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <returns>网关设备工厂实例</returns>
        public static IGatewayDeviceFactory CreateGatewayDeviceFactory(ILogger logger)
        {
            var deviceFactory = new DeviceFactory();
            return new GatewayDeviceFactory(deviceFactory, logger);
        }
    }
} 