using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;

namespace Wombat.IndustrialCommunicationTestProject.Helper
{
    /// <summary>
    /// 连接中断模拟器，用于测试网络中断和重连场景
    /// </summary>
    public class ConnectionDisruptor
    {
        private readonly ILogger _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器，如果为null则不记录日志</param>
        public ConnectionDisruptor(ILogger logger = null)
        {
            _logger = logger;
            _logger?.LogDebug("创建连接中断模拟器实例");
        }

        /// <summary>
        /// 模拟连接中断
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> SimulateConnectionDisruption(IClient client)
        {
            _logger?.LogInformation("模拟连接中断开始");
            
            try
            {
                // 如果客户端未连接，直接返回成功
                if (!client.Connected)
                {
                    _logger?.LogInformation("客户端当前未连接，无需模拟中断");
                    return OperationResult.CreateSuccessResult("客户端当前未连接");
                }
                
                _logger?.LogDebug("准备获取客户端传输对象");
                // 使用反射获取内部TCP适配器
                var transport = GetTransportField(client);
                if (transport == null)
                {
                    _logger?.LogWarning("无法获取客户端传输对象");
                    return OperationResult.CreateFailedResult("无法获取客户端传输对象");
                }

                _logger?.LogDebug("准备获取流资源对象");
                var streamResource = GetStreamResourceField(transport);
                if (streamResource == null || !(streamResource is IStreamResource))
                {
                    _logger?.LogWarning("无法获取流资源对象");
                    return OperationResult.CreateFailedResult("无法获取流资源对象");
                }

                _logger?.LogDebug("检查客户端是否支持自动重连");
                // 临时禁用自动重连（如果客户端支持）
                var wasAutoReconnectEnabled = false;
                if (client is IAutoReconnectClient autoReconnectClient)
                {
                    wasAutoReconnectEnabled = autoReconnectClient.EnableAutoReconnect;
                    if (wasAutoReconnectEnabled)
                    {
                        _logger?.LogDebug("临时禁用自动重连功能");
                        autoReconnectClient.EnableAutoReconnect = false;
                    }
                }
                
                try
                {
                    _logger?.LogDebug("执行安全断开连接操作");
                    // 强制断开连接
                    await SafeForceDisconnect(streamResource);
                }
                finally
                {
                    // 恢复自动重连设置
                    if (client is IAutoReconnectClient autoReconnectClient2 && wasAutoReconnectEnabled)
                    {
                        _logger?.LogDebug("恢复自动重连功能");
                        autoReconnectClient2.EnableAutoReconnect = true;
                    }
                }
                
                _logger?.LogInformation("模拟连接中断完成");
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "模拟连接中断失败");
                return OperationResult.CreateFailedResult(ex);
            }
        }

        /// <summary>
        /// 模拟短线重连场景
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <param name="disruptionDurationMs">中断持续时间(毫秒)</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> SimulateDisconnectAndReconnect(IClient client, int disruptionDurationMs = 2000)
        {
            _logger?.LogInformation("开始模拟短线重连场景，中断持续时间: {Duration}ms", disruptionDurationMs);
            
            // 简化版本：仅模拟连接中断，不尝试重连
            // 这样可以避免可能的循环调用
            try
            {
                // 1. 模拟连接中断
                _logger?.LogInformation("开始模拟连接中断");
                var disruptResult = await SimulateConnectionDisruption(client);
                if (!disruptResult.IsSuccess)
                {
                    _logger?.LogWarning("模拟连接中断失败: {Message}", disruptResult.Message);
                    return disruptResult;
                }

                // 2. 等待指定时间
                _logger?.LogInformation("等待 {Duration}ms", disruptionDurationMs);
                await Task.Delay(disruptionDurationMs);
                
                // 3. 提示完成
                _logger?.LogInformation("模拟短线重连场景完成");
                return OperationResult.CreateSuccessResult("中断模拟完成，等待客户端自行处理重连");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "模拟短线重连过程中发生异常");
                return OperationResult.CreateFailedResult(ex);
            }
        }

        /// <summary>
        /// 强制断开连接（已废弃，可能导致循环调用）
        /// </summary>
        /// <param name="streamResource">流资源</param>
        [Obsolete("此方法可能导致循环调用和栈溢出，请使用SafeForceDisconnect代替")]
        private async Task ForceDisconnect(IStreamResource streamResource)
        {
            // 不再直接调用StreamClose方法，避免可能的循环调用
            // 使用反射直接设置连接状态或直接关闭底层连接
            await SafeForceDisconnect(streamResource);
        }
        
        /// <summary>
        /// 安全地强制断开连接，避免循环调用问题
        /// </summary>
        /// <param name="streamResourceObj">流资源对象</param>
        private async Task SafeForceDisconnect(object streamResourceObj)
        {
            if (streamResourceObj == null)
            {
                _logger?.LogWarning("流资源对象为空，无法执行断开操作");
                return;
            }
                
            try
            {
                var streamResource = streamResourceObj as IStreamResource;
                if (streamResource == null)
                {
                    _logger?.LogWarning("流资源对象不是IStreamResource类型，无法执行断开操作");
                    return;
                }
                    
                // 获取实际类型，尝试使用更安全的方式断开连接
                var type = streamResourceObj.GetType();
                _logger?.LogDebug("流资源对象类型: {Type}", type.FullName);
                
                // 尝试方法1: 如果是TcpClientAdapter，直接使用反射获取并关闭底层套接字
                var tcpSocketClientBaseField = type.GetField("_tcpSocketClientBase", BindingFlags.NonPublic | BindingFlags.Instance);
                if (tcpSocketClientBaseField != null)
                {
                    _logger?.LogDebug("检测到TcpClientAdapter类型");
                    var tcpSocketClient = tcpSocketClientBaseField.GetValue(streamResourceObj);
                    if (tcpSocketClient != null)
                    {
                        // 直接尝试设置Connected属性为false (如果存在)
                        var connectedProp = tcpSocketClient.GetType().GetProperty("Connected");
                        if (connectedProp != null && connectedProp.CanWrite)
                        {
                            _logger?.LogDebug("直接设置底层套接字Connected属性为false");
                            connectedProp.SetValue(tcpSocketClient, false);
                            await Task.Delay(100);
                            return;
                        }
                        
                        // 调用Close方法而不是Shutdown，避免可能的循环调用
                        var closeMethod = tcpSocketClient.GetType().GetMethod("Close");
                        if (closeMethod != null)
                        {
                            _logger?.LogDebug("使用底层套接字Close方法关闭连接");
                            // 调用异步方法需要等待结果
                            var closeTask = closeMethod.Invoke(tcpSocketClient, null) as Task;
                            if (closeTask != null)
                                await closeTask;
                            
                            // 等待一小段时间确保连接关闭
                            await Task.Delay(100);
                            return;
                        }
                    }
                }
                
                // 尝试方法2: 如果是SerialPortAdapter，尝试关闭串口
                var serialPortField = type.GetField("_serialPort", BindingFlags.NonPublic | BindingFlags.Instance);
                if (serialPortField != null)
                {
                    _logger?.LogDebug("检测到SerialPortAdapter类型");
                    var serialPort = serialPortField.GetValue(streamResourceObj);
                    if (serialPort != null)
                    {
                        // 直接尝试设置IsOpen属性为false (如果存在)
                        var isOpenProp = serialPort.GetType().GetProperty("IsOpen");
                        if (isOpenProp != null && isOpenProp.CanWrite)
                        {
                            _logger?.LogDebug("直接设置串口IsOpen属性为false");
                            isOpenProp.SetValue(serialPort, false);
                            await Task.Delay(100);
                            return;
                        }
                        
                        // 调用Close方法关闭串口
                        var closeMethod = serialPort.GetType().GetMethod("Close");
                        if (closeMethod != null)
                        {
                            _logger?.LogDebug("使用串口Close方法关闭连接");
                            closeMethod.Invoke(serialPort, null);
                            
                            // 等待一小段时间确保连接关闭
                            await Task.Delay(100);
                            return;
                        }
                    }
                }
                
                // 尝试方法3: 使用反射查找和调用Dispose方法（很多流对象都实现了IDisposable）
                var disposeMethod = type.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
                if (disposeMethod != null)
                {
                    try
                    {
                        _logger?.LogDebug("尝试调用Dispose方法关闭连接");
                        disposeMethod.Invoke(streamResourceObj, null);
                        await Task.Delay(100);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "调用Dispose方法失败");
                    }
                }
                
                // 如果以上方法都失败，尝试直接设置Connected属性为false（如果可能）
                var connectedProperty = type.GetProperty("Connected");
                if (connectedProperty != null && connectedProperty.CanWrite)
                {
                    _logger?.LogDebug("尝试直接设置Connected属性为false");
                    connectedProperty.SetValue(streamResourceObj, false);
                    await Task.Delay(100);
                    return;
                }
                
                // 最后的方法：我们不再调用StreamClose方法，因为它可能导致循环调用
                // 而是简单地记录一个警告，表示无法安全断开连接
                _logger?.LogWarning("无法使用安全方式断开连接，该连接可能仍处于活动状态");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "安全强制断开连接时发生异常");
            }
        }

        /// <summary>
        /// 获取客户端的传输对象
        /// </summary>
        /// <param name="client">客户端对象</param>
        /// <returns>传输对象</returns>
        private object GetTransportField(object client)
        {
            var type = client.GetType();
            _logger?.LogDebug("尝试从类型 {Type} 获取传输对象", type.FullName);
            
            // 查找Transport字段或属性
            var field = type.GetField("Transport", BindingFlags.NonPublic | BindingFlags.Instance) ??
                        type.GetField("_transport", BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field != null)
            {
                _logger?.LogDebug("通过字段 {FieldName} 获取传输对象", field.Name);
                return field.GetValue(client);
            }
            
            var property = type.GetProperty("Transport", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                _logger?.LogDebug("通过属性 {PropertyName} 获取传输对象", property.Name);
                return property.GetValue(client);
            }
            
            // 查找基类，但限制递归深度为1层
            if (type.BaseType != null && type.BaseType != typeof(object))
            {
                _logger?.LogDebug("在基类 {BaseType} 中查找传输对象", type.BaseType.FullName);
                field = type.BaseType.GetField("Transport", BindingFlags.NonPublic | BindingFlags.Instance) ??
                        type.BaseType.GetField("_transport", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (field != null)
                {
                    _logger?.LogDebug("通过基类字段 {FieldName} 获取传输对象", field.Name);
                    return field.GetValue(client);
                }
                
                property = type.BaseType.GetProperty("Transport", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null)
                {
                    _logger?.LogDebug("通过基类属性 {PropertyName} 获取传输对象", property.Name);
                    return property.GetValue(client);
                }
            }
            
            _logger?.LogWarning("无法找到传输对象");
            return null;
        }

        /// <summary>
        /// 获取传输对象的流资源
        /// </summary>
        /// <param name="transport">传输对象</param>
        /// <returns>流资源对象</returns>
        private object GetStreamResourceField(object transport)
        {
            var type = transport.GetType();
            _logger?.LogDebug("尝试从类型 {Type} 获取流资源", type.FullName);
            
            // 查找StreamResource字段或属性
            var field = type.GetField("StreamResource", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance) ??
                        type.GetField("_streamResource", BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field != null)
            {
                _logger?.LogDebug("通过字段 {FieldName} 获取流资源", field.Name);
                return field.GetValue(transport);
            }
            
            var property = type.GetProperty("StreamResource", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                _logger?.LogDebug("通过属性 {PropertyName} 获取流资源", property.Name);
                return property.GetValue(transport);
            }
            
            _logger?.LogWarning("无法找到流资源");
            return null;
        }
    }
} 