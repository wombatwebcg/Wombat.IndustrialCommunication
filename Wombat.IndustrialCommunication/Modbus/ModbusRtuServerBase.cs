using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus.Data;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// Modbus RTU服务器基类，负责处理Modbus协议
    /// </summary>
    public class ModbusRtuServerBase : DeviceServerBase
    {
        private readonly AsyncLock _lock = new AsyncLock();
        protected readonly ServerMessageTransport _transport;
        
        /// <summary>
        /// 数据存储
        /// </summary>
        public DataStore DataStore { get; } = DataStoreFactory.CreateDefaultDataStore();

        /// <summary>
        /// 是否正在监听
        /// </summary>
        public bool IsListening => _transport?.StreamResource is SerialPortServerAdapter adapter && adapter.IsListening;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="transport">服务器消息传输</param>
        public ModbusRtuServerBase(ServerMessageTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            DataFormat = EndianFormat.ABCD;
            IsReverse = true;
            
            // 注册消息处理程序
            _transport.RegisterMessageHandler(HandleModbusMessage);
        }

        /// <summary>
        /// 版本
        /// </summary>
        public override string Version => nameof(ModbusRtuServerBase);

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <returns>操作结果</returns>
        public virtual async Task<OperationResult> StartAsync()
        {
            return await _transport.StartAsync();
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        /// <returns>操作结果</returns>
        public virtual async Task<OperationResult> StopAsync()
        {
            return await _transport.StopAsync();
        }

        /// <summary>
        /// 处理Modbus消息
        /// </summary>
        /// <param name="receivedMessage">接收到的消息</param>
        protected virtual void HandleModbusMessage(ReceivedMessage receivedMessage)
        {
            byte[] request = receivedMessage.Data;
            
            if (request == null || request.Length < 4) // 最小Modbus RTU帧: 地址(1) + 功能码(1) + CRC(2)
            {
                Logger?.LogWarning("接收到无效的Modbus RTU请求");
                return;
            }
            
            try
            {
                // 验证CRC
                if (!ValidateCrc(request))
                {
                    Logger?.LogWarning("接收到的Modbus RTU请求CRC校验失败");
                    return;
                }
                
                // 解析请求
                byte station = request[0];
                byte functionCode = request[1];
                
                // 提取数据区域（不包括站号、功能码和CRC）
                byte[] data = new byte[request.Length - 4];
                Array.Copy(request, 2, data, 0, data.Length);
                
                byte[] responseData = null;
                
                // 根据功能码处理不同类型的请求
                switch (functionCode)
                {
                    case 1: // 读线圈
                        responseData = HandleReadCoils(data);
                        break;
                    case 2: // 读离散量输入
                        responseData = HandleReadDiscreteInputs(data);
                        break;
                    case 3: // 读保持寄存器
                        responseData = HandleReadHoldingRegisters(data);
                        break;
                    case 4: // 读输入寄存器
                        responseData = HandleReadInputRegisters(data);
                        break;
                    case 5: // 写单个线圈
                        responseData = HandleWriteSingleCoil(data);
                        break;
                    case 6: // 写单个寄存器
                        responseData = HandleWriteSingleRegister(data);
                        break;
                    case 15: // 写多个线圈
                        responseData = HandleWriteMultipleCoils(data);
                        break;
                    case 16: // 写多个寄存器
                        responseData = HandleWriteMultipleRegisters(data);
                        break;
                    default:
                        // 不支持的功能码
                        responseData = CreateExceptionResponse(functionCode, 1); // 非法功能
                        break;
                }
                
                if (responseData != null)
                {
                    // 创建响应帧
                    var responseGenerator = new ModbusRTUResponseGenerator(station, functionCode, responseData);
                    byte[] response = responseGenerator.ResponseFrame;
                    
                    // 发送响应
                    _ = receivedMessage.Session.SendAsync(response);
                }
            }
            catch (InvalidModbusRequestException ex)
            {
                // 处理Modbus协议异常
                byte[] exceptionResponse = CreateExceptionResponse(request[1], ex.ExceptionCode);
                if (exceptionResponse != null)
                {
                    var responseGenerator = new ModbusRTUResponseGenerator(request[0], (byte)(request[1] | 0x80), new byte[] { ex.ExceptionCode });
                    _ = receivedMessage.Session.SendAsync(responseGenerator.ResponseFrame);
                }
                
                Logger?.LogWarning($"Modbus请求异常: {ex.Message}");
            }
            catch (Exception ex)
            {
                // 一般异常处理
                Logger?.LogError(ex, "处理Modbus RTU请求时发生异常");
            }
        }
        
        /// <summary>
        /// 验证CRC
        /// </summary>
        /// <param name="data">包含CRC的数据</param>
        /// <returns>CRC是否有效</returns>
        private bool ValidateCrc(byte[] data)
        {
            if (data.Length < 2)
                return false;
                
            // 计算CRC
            ushort calculatedCrc = CalculateCrc(data, 0, data.Length - 2);
            
            // 从数据中提取CRC（低字节在前，高字节在后）
            ushort receivedCrc = (ushort)((data[data.Length - 1] << 8) | data[data.Length - 2]);
            
            return calculatedCrc == receivedCrc;
        }
        
        /// <summary>
        /// 计算CRC
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="start">起始索引</param>
        /// <param name="length">长度</param>
        /// <returns>CRC值</returns>
        private ushort CalculateCrc(byte[] data, int start, int length)
        {
            ushort crc = 0xFFFF;
            
            for (int i = start; i < start + length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            
            return crc;
        }
        
        /// <summary>
        /// 创建异常响应
        /// </summary>
        /// <param name="functionCode">功能码</param>
        /// <param name="exceptionCode">异常码</param>
        /// <returns>异常响应数据</returns>
        private byte[] CreateExceptionResponse(byte functionCode, byte exceptionCode)
        {
            return new byte[] { exceptionCode };
        }
        
        /// <summary>
        /// 处理读线圈请求
        /// </summary>
        /// <param name="data">请求数据</param>
        /// <returns>响应数据</returns>
        private byte[] HandleReadCoils(byte[] data)
        {
            if (data.Length < 4)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 提取起始地址和数量
            ushort startAddress = (ushort)((data[0] << 8) | data[1]);
            ushort quantity = (ushort)((data[2] << 8) | data[3]);
            
            if (quantity == 0 || quantity > 2000)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 获取线圈状态
            bool[] coilValues = new bool[quantity];
            for (int i = 0; i < quantity; i++)
            {
                coilValues[i] = DataStore.CoilDiscretes[startAddress + i];
            }
            
            // 打包成字节
            byte byteCount = (byte)((quantity + 7) / 8);
            byte[] result = new byte[1 + byteCount];
            result[0] = byteCount;
            
            for (int i = 0; i < quantity; i++)
            {
                if (coilValues[i])
                {
                    result[1 + (i / 8)] |= (byte)(1 << (i % 8));
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 处理读离散量输入请求
        /// </summary>
        /// <param name="data">请求数据</param>
        /// <returns>响应数据</returns>
        private byte[] HandleReadDiscreteInputs(byte[] data)
        {
            if (data.Length < 4)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 提取起始地址和数量
            ushort startAddress = (ushort)((data[0] << 8) | data[1]);
            ushort quantity = (ushort)((data[2] << 8) | data[3]);
            
            if (quantity == 0 || quantity > 2000)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 获取离散量输入状态
            bool[] inputValues = new bool[quantity];
            for (int i = 0; i < quantity; i++)
            {
                inputValues[i] = DataStore.InputDiscretes[startAddress + i];
            }
            
            // 打包成字节
            byte byteCount = (byte)((quantity + 7) / 8);
            byte[] result = new byte[1 + byteCount];
            result[0] = byteCount;
            
            for (int i = 0; i < quantity; i++)
            {
                if (inputValues[i])
                {
                    result[1 + (i / 8)] |= (byte)(1 << (i % 8));
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 处理读保持寄存器请求
        /// </summary>
        /// <param name="data">请求数据</param>
        /// <returns>响应数据</returns>
        private byte[] HandleReadHoldingRegisters(byte[] data)
        {
            if (data.Length < 4)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 提取起始地址和数量
            ushort startAddress = (ushort)((data[0] << 8) | data[1]);
            ushort quantity = (ushort)((data[2] << 8) | data[3]);
            
            if (quantity == 0 || quantity > 125)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 获取保持寄存器值
            ushort[] registerValues = new ushort[quantity];
            for (int i = 0; i < quantity; i++)
            {
                registerValues[i] = (ushort)DataStore.HoldingRegisters[startAddress + i];
            }
            
            // 打包成字节
            byte byteCount = (byte)(quantity * 2);
            byte[] result = new byte[1 + byteCount];
            result[0] = byteCount;
            
            for (int i = 0; i < quantity; i++)
            {
                result[1 + i * 2] = (byte)(registerValues[i] >> 8); // 高字节
                result[1 + i * 2 + 1] = (byte)(registerValues[i] & 0xFF); // 低字节
            }
            
            return result;
        }
        
        /// <summary>
        /// 处理读输入寄存器请求
        /// </summary>
        /// <param name="data">请求数据</param>
        /// <returns>响应数据</returns>
        private byte[] HandleReadInputRegisters(byte[] data)
        {
            if (data.Length < 4)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 提取起始地址和数量
            ushort startAddress = (ushort)((data[0] << 8) | data[1]);
            ushort quantity = (ushort)((data[2] << 8) | data[3]);
            
            if (quantity == 0 || quantity > 125)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 获取输入寄存器值
            ushort[] registerValues = new ushort[quantity];
            for (int i = 0; i < quantity; i++)
            {
                registerValues[i] = (ushort)DataStore.InputRegisters[startAddress + i];
            }
            
            // 打包成字节
            byte byteCount = (byte)(quantity * 2);
            byte[] result = new byte[1 + byteCount];
            result[0] = byteCount;
            
            for (int i = 0; i < quantity; i++)
            {
                result[1 + i * 2] = (byte)(registerValues[i] >> 8); // 高字节
                result[1 + i * 2 + 1] = (byte)(registerValues[i] & 0xFF); // 低字节
            }
            
            return result;
        }
        
        /// <summary>
        /// 处理写单个线圈请求
        /// </summary>
        /// <param name="data">请求数据</param>
        /// <returns>响应数据</returns>
        private byte[] HandleWriteSingleCoil(byte[] data)
        {
            if (data.Length < 4)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 提取地址和值
            ushort address = (ushort)((data[0] << 8) | data[1]);
            ushort value = (ushort)((data[2] << 8) | data[3]);
            
            if (value != 0 && value != 0xFF00)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 设置线圈状态
            DataStore.CoilDiscretes[address] = value == 0xFF00;
            
            // 响应与请求相同
            return data;
        }
        
        /// <summary>
        /// 处理写单个寄存器请求
        /// </summary>
        /// <param name="data">请求数据</param>
        /// <returns>响应数据</returns>
        private byte[] HandleWriteSingleRegister(byte[] data)
        {
            if (data.Length < 4)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 提取地址和值
            ushort address = (ushort)((data[0] << 8) | data[1]);
            ushort value = (ushort)((data[2] << 8) | data[3]);
            
            // 设置寄存器值
            DataStore.HoldingRegisters[address] = value;
            
            // 响应与请求相同
            return data;
        }
        
        /// <summary>
        /// 处理写多个线圈请求
        /// </summary>
        /// <param name="data">请求数据</param>
        /// <returns>响应数据</returns>
        private byte[] HandleWriteMultipleCoils(byte[] data)
        {
            if (data.Length < 6)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 提取起始地址、数量和字节数
            ushort startAddress = (ushort)((data[0] << 8) | data[1]);
            ushort quantity = (ushort)((data[2] << 8) | data[3]);
            byte byteCount = data[4];
            
            if (quantity == 0 || quantity > 1968 || byteCount != (quantity + 7) / 8)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            if (data.Length < 5 + byteCount)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 设置线圈状态
            for (int i = 0; i < quantity; i++)
            {
                bool value = (data[5 + (i / 8)] & (1 << (i % 8))) != 0;
                DataStore.CoilDiscretes[startAddress + i] = value;
            }
            
            // 响应包含地址和数量
            byte[] response = new byte[4];
            response[0] = data[0]; // 起始地址高字节
            response[1] = data[1]; // 起始地址低字节
            response[2] = data[2]; // 数量高字节
            response[3] = data[3]; // 数量低字节
            
            return response;
        }
        
        /// <summary>
        /// 处理写多个寄存器请求
        /// </summary>
        /// <param name="data">请求数据</param>
        /// <returns>响应数据</returns>
        private byte[] HandleWriteMultipleRegisters(byte[] data)
        {
            if (data.Length < 6)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 提取起始地址、数量和字节数
            ushort startAddress = (ushort)((data[0] << 8) | data[1]);
            ushort quantity = (ushort)((data[2] << 8) | data[3]);
            byte byteCount = data[4];
            
            if (quantity == 0 || quantity > 123 || byteCount != quantity * 2)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            if (data.Length < 5 + byteCount)
                throw new InvalidModbusRequestException(3); // 非法数据值
                
            // 设置寄存器值
            for (int i = 0; i < quantity; i++)
            {
                ushort value = (ushort)((data[5 + i * 2] << 8) | data[5 + i * 2 + 1]);
                DataStore.HoldingRegisters[startAddress + i] = value;
            }
            
            // 响应包含地址和数量
            byte[] response = new byte[4];
            response[0] = data[0]; // 起始地址高字节
            response[1] = data[1]; // 起始地址低字节
            response[2] = data[2]; // 数量高字节
            response[3] = data[3]; // 数量低字节
            
            return response;
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 释放传输资源
                _transport?.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }
} 