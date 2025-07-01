using System;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// Modbus客户端扩展方法类，为IModbusClient提供便利的扩展方法
    /// </summary>
    public static class ModbusClientExtensions
    {
        #region 私有工具方法

        /// <summary>
        /// 构建标准的Modbus地址
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="address">地址</param>
        /// <returns>Modbus格式地址字符串</returns>
        private static string BuildModbusAddress(byte stationNumber, byte functionCode, ushort address)
        {
            return $"{stationNumber};{functionCode};{address}";
        }

        /// <summary>
        /// 将字节数组转换为布尔数组
        /// </summary>
        private static bool[] BytesToBoolArray(byte[] bytes, int count)
        {
            bool[] result = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int byteIndex = i / 8;
                int bitIndex = i % 8;
                if (byteIndex < bytes.Length)
                {
                    result[i] = (bytes[byteIndex] & (1 << bitIndex)) != 0;
                }
            }
            return result;
        }

        /// <summary>
        /// 将字节数组转换为ushort数组
        /// </summary>
        private static ushort[] BytesToUshortArray(byte[] bytes)
        {
            int count = bytes.Length / 2;
            ushort[] result = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = BitConverter.ToUInt16(new byte[] { bytes[i * 2 + 1], bytes[i * 2] }, 0);
            }
            return result;
        }

        #endregion

        #region 同步方法实现

        /// <summary>
        /// 读取线圈状态 (功能码 01)
        /// </summary>
        public static OperationResult<bool> ReadCoil(this IModbusClient client, byte stationNumber, ushort address)
        {
            try
            {
                // 通过反射或其他方式访问DeviceDataReaderWriterBase的方法
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 1, address);
                    var result = baseClient.ReadBoolean(modbusAddress);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<bool>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool>($"读取线圈失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取多个线圈状态 (功能码 01)
        /// </summary>
        public static OperationResult<bool[]> ReadCoils(this IModbusClient client, byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 1, startAddress);
                    var result = baseClient.ReadBoolean(modbusAddress, count);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<bool[]>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool[]>($"读取多个线圈失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取离散输入状态 (功能码 02)
        /// </summary>
        public static OperationResult<bool> ReadDiscreteInput(this IModbusClient client, byte stationNumber, ushort address)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 2, address);
                    var result = baseClient.ReadBoolean(modbusAddress);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<bool>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool>($"读取离散输入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取多个离散输入状态 (功能码 02)
        /// </summary>
        public static OperationResult<bool[]> ReadDiscreteInputs(this IModbusClient client, byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 2, startAddress);
                    var result = baseClient.ReadBoolean(modbusAddress, count);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<bool[]>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool[]>($"读取多个离散输入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取保持寄存器 (功能码 03)
        /// </summary>
        public static OperationResult<ushort> ReadHoldingRegister(this IModbusClient client, byte stationNumber, ushort address)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 3, address);
                    var result = baseClient.ReadUInt16(modbusAddress);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<ushort>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort>($"读取保持寄存器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取多个保持寄存器 (功能码 03)
        /// </summary>
        public static OperationResult<ushort[]> ReadHoldingRegisters(this IModbusClient client, byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 3, startAddress);
                    var result = baseClient.ReadUInt16(modbusAddress, count);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<ushort[]>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort[]>($"读取多个保持寄存器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取输入寄存器 (功能码 04)
        /// </summary>
        public static OperationResult<ushort> ReadInputRegister(this IModbusClient client, byte stationNumber, ushort address)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 4, address);
                    var result = baseClient.ReadUInt16(modbusAddress);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<ushort>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort>($"读取输入寄存器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取多个输入寄存器 (功能码 04)
        /// </summary>
        public static OperationResult<ushort[]> ReadInputRegisters(this IModbusClient client, byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 4, startAddress);
                    var result = baseClient.ReadUInt16(modbusAddress, count);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<ushort[]>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort[]>($"读取多个输入寄存器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入单个线圈 (功能码 05)
        /// </summary>
        public static OperationResult WriteCoil(this IModbusClient client, byte stationNumber, ushort address, bool value)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 5, address);
                    var result = baseClient.Write(modbusAddress, value);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"写入线圈失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入多个线圈 (功能码 15)
        /// </summary>
        public static OperationResult WriteCoils(this IModbusClient client, byte stationNumber, ushort startAddress, bool[] values)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 15, startAddress);
                    var result = baseClient.Write(modbusAddress, values);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"写入多个线圈失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入单个保持寄存器 (功能码 06)
        /// </summary>
        public static OperationResult WriteHoldingRegister(this IModbusClient client, byte stationNumber, ushort address, ushort value)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 6, address);
                    var result = baseClient.Write(modbusAddress, value);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"写入保持寄存器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入多个保持寄存器 (功能码 16)
        /// </summary>
        public static OperationResult WriteHoldingRegisters(this IModbusClient client, byte stationNumber, ushort startAddress, ushort[] values)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 16, startAddress);
                    var result = baseClient.Write(modbusAddress, values);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"写入多个保持寄存器失败: {ex.Message}");
            }
        }

        #endregion

        #region 异步方法实现

        /// <summary>
        /// 异步读取线圈状态 (功能码 01)
        /// </summary>
        public static async Task<OperationResult<bool>> ReadCoilAsync(this IModbusClient client, byte stationNumber, ushort address)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 1, address);
                    var result = await baseClient.ReadBooleanAsync(modbusAddress);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<bool>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool>($"异步读取线圈失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步读取多个线圈状态 (功能码 01)
        /// </summary>
        public static async Task<OperationResult<bool[]>> ReadCoilsAsync(this IModbusClient client, byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 1, startAddress);
                    var result = await baseClient.ReadBooleanAsync(modbusAddress, count);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<bool[]>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool[]>($"异步读取多个线圈失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步读取离散输入状态 (功能码 02)
        /// </summary>
        public static async Task<OperationResult<bool>> ReadDiscreteInputAsync(this IModbusClient client, byte stationNumber, ushort address)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 2, address);
                    var result = await baseClient.ReadBooleanAsync(modbusAddress);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<bool>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool>($"异步读取离散输入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步读取多个离散输入状态 (功能码 02)
        /// </summary>
        public static async Task<OperationResult<bool[]>> ReadDiscreteInputsAsync(this IModbusClient client, byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 2, startAddress);
                    var result = await baseClient.ReadBooleanAsync(modbusAddress, count);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<bool[]>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool[]>($"异步读取多个离散输入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步读取保持寄存器 (功能码 03)
        /// </summary>
        public static async Task<OperationResult<ushort>> ReadHoldingRegisterAsync(this IModbusClient client, byte stationNumber, ushort address)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 3, address);
                    var result = await baseClient.ReadUInt16Async(modbusAddress);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<ushort>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort>($"异步读取保持寄存器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步读取多个保持寄存器 (功能码 03)
        /// </summary>
        public static async Task<OperationResult<ushort[]>> ReadHoldingRegistersAsync(this IModbusClient client, byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 3, startAddress);
                    var result = await baseClient.ReadUInt16Async(modbusAddress, count);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<ushort[]>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort[]>($"异步读取多个保持寄存器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步读取输入寄存器 (功能码 04)
        /// </summary>
        public static async Task<OperationResult<ushort>> ReadInputRegisterAsync(this IModbusClient client, byte stationNumber, ushort address)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 4, address);
                    var result = await baseClient.ReadUInt16Async(modbusAddress);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<ushort>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort>($"异步读取输入寄存器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步读取多个输入寄存器 (功能码 04)
        /// </summary>
        public static async Task<OperationResult<ushort[]>> ReadInputRegistersAsync(this IModbusClient client, byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 4, startAddress);
                    var result = await baseClient.ReadUInt16Async(modbusAddress, count);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult<ushort[]>("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort[]>($"异步读取多个输入寄存器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步写入单个线圈 (功能码 05)
        /// </summary>
        public static async Task<OperationResult> WriteCoilAsync(this IModbusClient client, byte stationNumber, ushort address, bool value)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 5, address);
                    var result = await baseClient.WriteAsync(modbusAddress, value);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"异步写入线圈失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步写入多个线圈 (功能码 15)
        /// </summary>
        public static async Task<OperationResult> WriteCoilsAsync(this IModbusClient client, byte stationNumber, ushort startAddress, bool[] values)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 15, startAddress);
                    var result = await baseClient.WriteAsync(modbusAddress, values);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"异步写入多个线圈失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步写入单个保持寄存器 (功能码 06)
        /// </summary>
        public static async Task<OperationResult> WriteHoldingRegisterAsync(this IModbusClient client, byte stationNumber, ushort address, ushort value)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 6, address);
                    var result = await baseClient.WriteAsync(modbusAddress, value);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"异步写入保持寄存器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步写入多个保持寄存器 (功能码 16)
        /// </summary>
        public static async Task<OperationResult> WriteHoldingRegistersAsync(this IModbusClient client, byte stationNumber, ushort startAddress, ushort[] values)
        {
            try
            {
                if (client is DeviceDataReaderWriterBase baseClient)
                {
                    string modbusAddress = BuildModbusAddress(stationNumber, 16, startAddress);
                    var result = await baseClient.WriteAsync(modbusAddress, values);
                    return result;
                }
                else
                {
                    return OperationResult.CreateFailedResult("客户端类型不支持此操作");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"异步写入多个保持寄存器失败: {ex.Message}");
            }
        }

        #endregion
    }
} 