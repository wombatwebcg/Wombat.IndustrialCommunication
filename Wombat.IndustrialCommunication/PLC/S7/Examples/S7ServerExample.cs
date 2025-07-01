using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication.PLC.Examples
{
    /// <summary>
    /// S7TcpServer示例代码
    /// 展示如何使用S7TcpServer的数据区操作接口
    /// </summary>
    public class S7ServerExample
    {
        private readonly ILogger _logger;
        private readonly S7TcpServer _server;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public S7ServerExample(ILogger logger = null)
        {
            _logger = logger;
            
            // 创建S7TcpServer实例
            _server = new S7TcpServer("0.0.0.0", 102);
            if (logger != null)
            {
                _server.UseLogger(logger);
            }
        }
        
        /// <summary>
        /// 运行示例
        /// </summary>
        public async Task RunExample()
        {
            try
            {
                // 启动服务器
                var result = _server.Listen();
                if (!result.IsSuccess)
                {
                    LogMessage("启动S7服务器失败: " + result.Message);
                    return;
                }
                
                LogMessage("S7服务器已启动，监听端口: 102");
                
                // 重置所有数据区
                _server.ResetAllDataAreas();
                
                // 创建和管理DB块
                await CreateAndManageDataBlocks();
                
                // 访问各个数据区域
                await AccessDataAreas();
                
                // 高级操作
                await AdvancedOperations();
                
                // 停止服务器
                _server.Shutdown();
                LogMessage("S7服务器已停止");
            }
            catch (Exception ex)
            {
                LogMessage("示例运行出错: " + ex.Message);
            }
            finally
            {
                // 确保服务器已关闭
                _server.Dispose();
            }
        }
        
        /// <summary>
        /// 创建和管理DB块
        /// </summary>
        private async Task CreateAndManageDataBlocks()
        {
            LogMessage("========== 创建和管理DB块 ==========");
            
            // 创建DB1 (1KB)
            var result = _server.CreateDataBlock(1, 1024);
            LogMessage($"创建DB1 (1KB): {(result.IsSuccess ? "成功" : "失败: " + result.Message)}");
            
            // 创建DB2 (2KB)
            result = _server.CreateDataBlock(2, 2048);
            LogMessage($"创建DB2 (2KB): {(result.IsSuccess ? "成功" : "失败: " + result.Message)}");
            
            // 创建DB3 (4KB)
            result = _server.CreateDataBlock(3, 4096);
            LogMessage($"创建DB3 (4KB): {(result.IsSuccess ? "成功" : "失败: " + result.Message)}");
            
            // 获取所有DB块编号
            var dbNumbers = _server.GetDataBlockNumbers();
            LogMessage($"已创建的DB块编号: {string.Join(", ", dbNumbers)}");
            
            // 获取DB1大小
            var sizeResult = _server.GetDataBlockSize(1);
            if (sizeResult.IsSuccess)
            {
                LogMessage($"DB1大小: {sizeResult.ResultValue} 字节");
            }
            
            // 向DB1写入数据
            byte[] dataToWrite = new byte[10] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            result = _server.WriteDB(1, 0, dataToWrite);
            LogMessage($"向DB1写入数据: {(result.IsSuccess ? "成功" : "失败: " + result.Message)}");
            
            // 从DB1读取数据
            var readResult = _server.ReadDB(1, 0, 10);
            if (readResult.IsSuccess)
            {
                LogMessage($"从DB1读取的数据: {BitConverter.ToString(readResult.ResultValue)}");
            }
            
            // 删除DB3
            result = _server.DeleteDataBlock(3);
            LogMessage($"删除DB3: {(result.IsSuccess ? "成功" : "失败: " + result.Message)}");
            
            // 重新获取所有DB块编号
            dbNumbers = _server.GetDataBlockNumbers();
            LogMessage($"删除后的DB块编号: {string.Join(", ", dbNumbers)}");
            
            await Task.Delay(100); // 模拟异步操作
        }
        
        /// <summary>
        /// 访问各个数据区域
        /// </summary>
        private async Task AccessDataAreas()
        {
            LogMessage("========== 访问各个数据区域 ==========");
            
            // 向I区写入数据
            byte[] inputData = new byte[10] { 11, 22, 33, 44, 55, 66, 77, 88, 99, 100 };
            var result = _server.WriteInputs(0, inputData);
            LogMessage($"向I区写入数据: {(result.IsSuccess ? "成功" : "失败: " + result.Message)}");
            
            // 从I区读取数据
            var readResult = _server.ReadInputs(0, 10);
            if (readResult.IsSuccess)
            {
                LogMessage($"从I区读取的数据: {BitConverter.ToString(readResult.ResultValue)}");
            }
            
            // 向Q区写入数据
            byte[] outputData = new byte[5] { 101, 102, 103, 104, 105 };
            result = _server.WriteOutputs(0, outputData);
            LogMessage($"向Q区写入数据: {(result.IsSuccess ? "成功" : "失败: " + result.Message)}");
            
            // 从Q区读取数据
            readResult = _server.ReadOutputs(0, 5);
            if (readResult.IsSuccess)
            {
                LogMessage($"从Q区读取的数据: {BitConverter.ToString(readResult.ResultValue)}");
            }
            
            // 向M区写入数据
            byte[] merkerData = new byte[8] { 201, 202, 203, 204, 205, 206, 207, 208 };
            result = _server.WriteMerkers(100, merkerData);
            LogMessage($"向M区写入数据: {(result.IsSuccess ? "成功" : "失败: " + result.Message)}");
            
            // 从M区读取数据
            readResult = _server.ReadMerkers(100, 8);
            if (readResult.IsSuccess)
            {
                LogMessage($"从M区读取的数据: {BitConverter.ToString(readResult.ResultValue)}");
            }
            
            // 使用通用方法访问T区
            byte[] timerData = new byte[4] { 50, 51, 52, 53 };
            result = _server.WriteArea(S7Area.T, 0, 0, timerData);
            LogMessage($"向T区写入数据: {(result.IsSuccess ? "成功" : "失败: " + result.Message)}");
            
            // 从T区读取数据
            readResult = _server.ReadArea(S7Area.T, 0, 0, 4);
            if (readResult.IsSuccess)
            {
                LogMessage($"从T区读取的数据: {BitConverter.ToString(readResult.ResultValue)}");
            }
            
            await Task.Delay(100); // 模拟异步操作
        }
        
        /// <summary>
        /// 高级操作
        /// </summary>
        private async Task AdvancedOperations()
        {
            LogMessage("========== 高级操作 ==========");
            
            // 创建更多DB块
            _server.CreateDataBlock(10, 512);
            _server.CreateDataBlock(20, 512);
            
            // 获取所有DB块编号
            var dbNumbers = _server.GetDataBlockNumbers();
            LogMessage($"所有DB块编号: {string.Join(", ", dbNumbers)}");
            
            // 清除所有DB块
            var result = _server.ClearAllDataBlocks();
            LogMessage($"清除所有DB块: {(result.IsSuccess ? "成功" : "失败: " + result.Message)}");
            
            // 重新获取所有DB块编号
            dbNumbers = _server.GetDataBlockNumbers();
            LogMessage($"清除后的DB块编号: {string.Join(", ", dbNumbers)}");
            
            // 重置所有数据区
            result = _server.ResetAllDataAreas();
            LogMessage($"重置所有数据区: {(result.IsSuccess ? "成功" : "失败: " + result.Message)}");
            
            // 验证I区是否已重置
            var readResult = _server.ReadInputs(0, 10);
            if (readResult.IsSuccess)
            {
                LogMessage($"重置后从I区读取的数据: {BitConverter.ToString(readResult.ResultValue)}");
            }
            
            await Task.Delay(100); // 模拟异步操作
        }
        
        /// <summary>
        /// 记录日志信息
        /// </summary>
        /// <param name="message">消息内容</param>
        private void LogMessage(string message)
        {
            if (_logger != null)
            {
                _logger.LogInformation(message);
            }
            else
            {
                Console.WriteLine(message);
            }
        }
    }
} 