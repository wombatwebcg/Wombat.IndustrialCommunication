using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus.Data;


namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// Modbus TCP服务器基类，负责处理Modbus协议
    /// </summary>
    public class ModbusTcpServerBase : DeviceServerBase
    {
        private readonly AsyncLock _lock = new AsyncLock();
        protected readonly ServerMessageTransport _transport;
        
        /// <summary>
        /// 数据存储
        /// </summary>
        public DataStore DataStore { get; } = DataStoreFactory.CreateDefaultDataStore();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="transport">服务器消息传输</param>
        public ModbusTcpServerBase(ServerMessageTransport transport)
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
        public override string Version => nameof(ModbusTcpServerBase);

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
        /// 是否正在监听
        /// </summary>
        public virtual bool IsListening => _transport.StreamResource.Connected;

        /// <summary>
        /// 处理Modbus消息
        /// </summary>
        /// <param name="message">接收到的消息</param>
        private void HandleModbusMessage(ReceivedMessage message)
        {
            if (message == null || message.Data == null || message.Data.Length < 8)
            {
                Logger?.LogWarning("收到无效的Modbus消息");
                return;
            }

            try
            {
                // 解析Modbus消息头
                ushort transactionId = (ushort)((message.Data[0] << 8) | message.Data[1]);
                ushort protocolId = (ushort)((message.Data[2] << 8) | message.Data[3]);
                ushort length = (ushort)((message.Data[4] << 8) | message.Data[5]);
                byte unitId = message.Data[6];
                byte functionCode = message.Data[7];

                Logger?.LogDebug(
                    "收到Modbus请求: TransactionId={TransactionId}, ProtocolId={ProtocolId}, Length={Length}, UnitId={UnitId}, FunctionCode={FunctionCode:X2}",
                    transactionId, protocolId, length, unitId, functionCode);

                // 处理Modbus请求并生成响应
                byte[] response = ProcessModbusRequest(message.Data, transactionId, protocolId, unitId);

                // 向客户端发送响应
                if (response != null)
                {
                    Task.Run(async () =>
                    {
                        var result = await _transport.SendToSessionAsync(message.Session, response);
                        if (!result.IsSuccess)
                        {
                            Logger?.LogError("发送Modbus响应失败: {ErrorMessage}", result.Message);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理Modbus消息时发生错误");
            }
        }

        /// <summary>
        /// 处理Modbus请求并生成响应
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <param name="transactionId">事务ID</param>
        /// <param name="protocolId">协议ID</param>
        /// <param name="unitId">单元ID</param>
        /// <returns>响应数据</returns>
        protected virtual byte[] ProcessModbusRequest(byte[] request, ushort transactionId, ushort protocolId, byte unitId)
        {
            if (request == null || request.Length < 8)
                return null;

            // 获取功能码
            byte functionCode = request[7];

            try
            {
                // 根据功能码处理不同类型的请求
                switch (functionCode)
                {
                    case 0x01: // 读线圈
                        return HandleReadCoilsRequest(request, transactionId, protocolId, unitId);

                    case 0x02: // 读离散输入
                        return HandleReadDiscreteInputsRequest(request, transactionId, protocolId, unitId);

                    case 0x03: // 读保持寄存器
                        return HandleReadHoldingRegistersRequest(request, transactionId, protocolId, unitId);

                    case 0x04: // 读输入寄存器
                        return HandleReadInputRegistersRequest(request, transactionId, protocolId, unitId);

                    case 0x05: // 写单个线圈
                        return HandleWriteSingleCoilRequest(request, transactionId, protocolId, unitId);

                    case 0x06: // 写单个寄存器
                        return HandleWriteSingleRegisterRequest(request, transactionId, protocolId, unitId);

                    case 0x0F: // 写多个线圈
                        return HandleWriteMultipleCoilsRequest(request, transactionId, protocolId, unitId);

                    case 0x10: // 写多个寄存器
                        return HandleWriteMultipleRegistersRequest(request, transactionId, protocolId, unitId);

                    default:
                        // 不支持的功能码
                        return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x01);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理Modbus请求时发生错误: FunctionCode={FunctionCode:X2}", functionCode);
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x04); // 服务器故障
            }
        }

        /// <summary>
        /// 创建异常响应
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <param name="transactionId">事务ID</param>
        /// <param name="protocolId">协议ID</param>
        /// <param name="unitId">单元ID</param>
        /// <param name="exceptionCode">异常码</param>
        /// <returns>异常响应数据</returns>
        protected byte[] CreateExceptionResponse(byte[] request, ushort transactionId, ushort protocolId, byte unitId, byte exceptionCode)
        {
            byte functionCode = request[7];
            
            // 创建异常响应
            byte[] response = new byte[9];
            
            // 事务ID
            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            
            // 协议ID
            response[2] = (byte)(protocolId >> 8);
            response[3] = (byte)(protocolId & 0xFF);
            
            // 长度
            response[4] = 0;
            response[5] = 3;
            
            // 单元ID
            response[6] = unitId;
            
            // 异常功能码
            response[7] = (byte)(functionCode | 0x80);
            
            // 异常码
            response[8] = exceptionCode;
            
            return response;
        }

        #region 功能码处理方法

        /// <summary>
        /// 处理读线圈请求
        /// </summary>
        protected virtual byte[] HandleReadCoilsRequest(byte[] request, ushort transactionId, ushort protocolId, byte unitId)
        {
            // 检查请求长度
            if (request.Length < 12)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03); // 非法数据值

            // 获取请求参数
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            // 检查数量范围
            if (quantity < 1 || quantity > 2000)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03); // 非法数据值

            try
            {
                // 从数据存储中读取数据
                bool[] values = new bool[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    values[i] = DataStore.CoilDiscretes[startAddress + i];
                }

                // 计算响应长度
                int byteCount = (quantity + 7) / 8;
                
                // 创建响应
                byte[] response = new byte[9 + byteCount];
                
                // 事务ID
                response[0] = (byte)(transactionId >> 8);
                response[1] = (byte)(transactionId & 0xFF);
                
                // 协议ID
                response[2] = (byte)(protocolId >> 8);
                response[3] = (byte)(protocolId & 0xFF);
                
                // 长度
                response[4] = (byte)((3 + byteCount) >> 8);
                response[5] = (byte)((3 + byteCount) & 0xFF);
                
                // 单元ID
                response[6] = unitId;
                
                // 功能码
                response[7] = 0x01;
                
                // 字节计数
                response[8] = (byte)byteCount;
                
                // 填充数据
                for (int i = 0; i < quantity; i++)
                {
                    if (values[i])
                    {
                        response[9 + i / 8] |= (byte)(1 << (i % 8));
                    }
                }
                
                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理读线圈请求时发生错误");
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x04); // 服务器故障
            }
        }

        /// <summary>
        /// 处理读离散输入请求
        /// </summary>
        protected virtual byte[] HandleReadDiscreteInputsRequest(byte[] request, ushort transactionId, ushort protocolId, byte unitId)
        {
            // 类似于HandleReadCoilsRequest，但使用DataStore.InputDiscretes
            if (request.Length < 12)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            if (quantity < 1 || quantity > 2000)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            try
            {
                bool[] values = new bool[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    values[i] = DataStore.InputDiscretes[startAddress + i];
                }

                int byteCount = (quantity + 7) / 8;
                byte[] response = new byte[9 + byteCount];

                response[0] = (byte)(transactionId >> 8);
                response[1] = (byte)(transactionId & 0xFF);
                response[2] = (byte)(protocolId >> 8);
                response[3] = (byte)(protocolId & 0xFF);
                response[4] = (byte)((3 + byteCount) >> 8);
                response[5] = (byte)((3 + byteCount) & 0xFF);
                response[6] = unitId;
                response[7] = 0x02;
                response[8] = (byte)byteCount;

                for (int i = 0; i < quantity; i++)
                {
                    if (values[i])
                    {
                        response[9 + i / 8] |= (byte)(1 << (i % 8));
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理读离散输入请求时发生错误");
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x04);
            }
        }

        /// <summary>
        /// 处理读保持寄存器请求
        /// </summary>
        protected virtual byte[] HandleReadHoldingRegistersRequest(byte[] request, ushort transactionId, ushort protocolId, byte unitId)
        {
            // 检查请求长度
            if (request.Length < 12)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            // 获取请求参数
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            // 检查数量范围
            if (quantity < 1 || quantity > 125)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            try
            {
                // 从数据存储中读取数据
                ushort[] values = new ushort[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    values[i] = (ushort)DataStore.HoldingRegisters[startAddress + i];
                }

                // 计算响应长度
                int byteCount = quantity * 2;
                
                // 创建响应
                byte[] response = new byte[9 + byteCount];
                
                // 事务ID
                response[0] = (byte)(transactionId >> 8);
                response[1] = (byte)(transactionId & 0xFF);
                
                // 协议ID
                response[2] = (byte)(protocolId >> 8);
                response[3] = (byte)(protocolId & 0xFF);
                
                // 长度
                response[4] = (byte)((3 + byteCount) >> 8);
                response[5] = (byte)((3 + byteCount) & 0xFF);
                
                // 单元ID
                response[6] = unitId;
                
                // 功能码
                response[7] = 0x03;
                
                // 字节计数
                response[8] = (byte)byteCount;
                
                // 填充数据
                for (int i = 0; i < quantity; i++)
                {
                    response[9 + i * 2] = (byte)(values[i] >> 8);
                    response[10 + i * 2] = (byte)(values[i] & 0xFF);
                }
                
                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理读保持寄存器请求时发生错误");
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x04);
            }
        }

        /// <summary>
        /// 处理读输入寄存器请求
        /// </summary>
        protected virtual byte[] HandleReadInputRegistersRequest(byte[] request, ushort transactionId, ushort protocolId, byte unitId)
        {
            // 类似于HandleReadHoldingRegistersRequest，但使用DataStore.InputRegisters
            if (request.Length < 12)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            if (quantity < 1 || quantity > 125)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            try
            {
                ushort[] values = new ushort[quantity];
                for (int i = 0; i < quantity; i++)
                {
                    values[i] = (ushort)DataStore.InputRegisters[startAddress + i];
                }

                int byteCount = quantity * 2;
                byte[] response = new byte[9 + byteCount];

                response[0] = (byte)(transactionId >> 8);
                response[1] = (byte)(transactionId & 0xFF);
                response[2] = (byte)(protocolId >> 8);
                response[3] = (byte)(protocolId & 0xFF);
                response[4] = (byte)((3 + byteCount) >> 8);
                response[5] = (byte)((3 + byteCount) & 0xFF);
                response[6] = unitId;
                response[7] = 0x04;
                response[8] = (byte)byteCount;

                for (int i = 0; i < quantity; i++)
                {
                    response[9 + i * 2] = (byte)(values[i] >> 8);
                    response[10 + i * 2] = (byte)(values[i] & 0xFF);
                }

                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理读输入寄存器请求时发生错误");
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x04);
            }
        }

        /// <summary>
        /// 处理写单个线圈请求
        /// </summary>
        protected virtual byte[] HandleWriteSingleCoilRequest(byte[] request, ushort transactionId, ushort protocolId, byte unitId)
        {
            // 检查请求长度
            if (request.Length < 12)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            // 获取请求参数
            ushort address = (ushort)((request[8] << 8) | request[9]);
            ushort value = (ushort)((request[10] << 8) | request[11]);

            // 检查值
            if (value != 0 && value != 0xFF00)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            try
            {
                // 写入数据
                DataStore.CoilDiscretes[address] = value == 0xFF00;

                // 创建响应（与请求相同）
                byte[] response = new byte[12];
                Array.Copy(request, response, 12);
                
                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理写单个线圈请求时发生错误");
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x04);
            }
        }

        /// <summary>
        /// 处理写单个寄存器请求
        /// </summary>
        protected virtual byte[] HandleWriteSingleRegisterRequest(byte[] request, ushort transactionId, ushort protocolId, byte unitId)
        {
            // 检查请求长度
            if (request.Length < 12)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            // 获取请求参数
            ushort address = (ushort)((request[8] << 8) | request[9]);
            ushort value = (ushort)((request[10] << 8) | request[11]);

            try
            {
                // 写入数据
                DataStore.HoldingRegisters[address] = value;

                // 创建响应（与请求相同）
                byte[] response = new byte[12];
                Array.Copy(request, response, 12);
                
                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理写单个寄存器请求时发生错误");
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x04);
            }
        }

        /// <summary>
        /// 处理写多个线圈请求
        /// </summary>
        protected virtual byte[] HandleWriteMultipleCoilsRequest(byte[] request, ushort transactionId, ushort protocolId, byte unitId)
        {
            // 检查请求长度
            if (request.Length < 14)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            // 获取请求参数
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);
            byte byteCount = request[12];

            // 检查数量范围和字节计数
            if (quantity < 1 || quantity > 1968 || byteCount != (quantity + 7) / 8)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            try
            {
                // 写入数据
                for (int i = 0; i < quantity; i++)
                {
                    byte byteValue = request[13 + i / 8];
                    bool bitValue = (byteValue & (1 << (i % 8))) != 0;
                    DataStore.CoilDiscretes[startAddress + i] = bitValue;
                }

                // 创建响应
                byte[] response = new byte[12];
                
                // 事务ID
                response[0] = (byte)(transactionId >> 8);
                response[1] = (byte)(transactionId & 0xFF);
                
                // 协议ID
                response[2] = (byte)(protocolId >> 8);
                response[3] = (byte)(protocolId & 0xFF);
                
                // 长度
                response[4] = 0;
                response[5] = 6;
                
                // 单元ID
                response[6] = unitId;
                
                // 功能码
                response[7] = 0x0F;
                
                // 起始地址
                response[8] = (byte)(startAddress >> 8);
                response[9] = (byte)(startAddress & 0xFF);
                
                // 数量
                response[10] = (byte)(quantity >> 8);
                response[11] = (byte)(quantity & 0xFF);
                
                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理写多个线圈请求时发生错误");
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x04);
            }
        }

        /// <summary>
        /// 处理写多个寄存器请求
        /// </summary>
        protected virtual byte[] HandleWriteMultipleRegistersRequest(byte[] request, ushort transactionId, ushort protocolId, byte unitId)
        {
            // 检查请求长度
            if (request.Length < 14)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            // 获取请求参数
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);
            byte byteCount = request[12];

            // 检查数量范围和字节计数
            if (quantity < 1 || quantity > 123 || byteCount != quantity * 2)
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x03);

            try
            {
                // 写入数据
                for (int i = 0; i < quantity; i++)
                {
                    ushort value = (ushort)((request[13 + i * 2] << 8) | request[14 + i * 2]);
                    DataStore.HoldingRegisters[startAddress + i] = value;
                }

                // 创建响应
                byte[] response = new byte[12];
                
                // 事务ID
                response[0] = (byte)(transactionId >> 8);
                response[1] = (byte)(transactionId & 0xFF);
                
                // 协议ID
                response[2] = (byte)(protocolId >> 8);
                response[3] = (byte)(protocolId & 0xFF);
                
                // 长度
                response[4] = 0;
                response[5] = 6;
                
                // 单元ID
                response[6] = unitId;
                
                // 功能码
                response[7] = 0x10;
                
                // 起始地址
                response[8] = (byte)(startAddress >> 8);
                response[9] = (byte)(startAddress & 0xFF);
                
                // 数量
                response[10] = (byte)(quantity >> 8);
                response[11] = (byte)(quantity & 0xFF);
                
                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理写多个寄存器请求时发生错误");
                return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x04);
            }
        }

        #endregion
    }
} 