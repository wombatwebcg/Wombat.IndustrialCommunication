using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusTcpClientBase : DeviceDataReaderWriterBase
    {
        private AsyncLock _lock = new AsyncLock();
        private volatile int _transactionId = 0;
        public ModbusTcpClientBase(DeviceMessageTransport transport):base(transport)
        {
            DataFormat = Extensions.DataTypeExtensions.EndianFormat.ABCD;
            IsReverse = true;

        }

        public override string Version => nameof(ModbusTcpClientBase);

        // 批量读写相关结构体
        protected struct ModbusAddressInfo
        {
            public string OriginalAddress { get; set; }
            public byte StationNumber { get; set; }
            public byte FunctionCode { get; set; }
            public ushort Address { get; set; }
            public int Length { get; set; }
            public DataTypeEnums DataType { get; set; }
        }
        protected class ModbusAddressBlock
        {
            public byte StationNumber { get; set; }
            public byte FunctionCode { get; set; }
            public ushort StartAddress { get; set; }
            public ushort TotalLength { get; set; }
            public List<ModbusAddressInfo> Addresses { get; set; } = new List<ModbusAddressInfo>();
            public double EfficiencyRatio { get; set; }
        }

        internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length,DataTypeEnums dataType, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusAddress(address, dataType, false, out var modbusAddress))
                {
                    var request = new ModbusTcpRequest(GenerateTransactionId(), modbusAddress.StationNumber,modbusAddress.FunctionCode, modbusAddress.Address, (ushort)length);
                    var response = await Transport.UnicastReadMessageAsync(request);
                    if (response.IsSuccess)
                    {
                        var dataPackage = response.ResultValue.ProtocolMessageFrame;
                        var modbusTcpResponse = new ModbusTcpResponse(dataPackage);
                        return new OperationResult<byte[]>(response, modbusTcpResponse.Data).Complete();
                    }

                }
                return OperationResult.CreateFailedResult<byte[]>(result);

            }
        }


        internal override async Task<OperationResult> WriteAsync(string address, byte[] data,DataTypeEnums dataType, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusAddress(address, dataType, true, out var modbusAddress))
                {
                    ushort registerOrCoilCount;
                    if (isBit || modbusAddress.FunctionCode == 0x05 || modbusAddress.FunctionCode == 0x0F)
                    {
                        // 位操作，长度就是线圈数量
                        registerOrCoilCount = (ushort)data.Length;
                    }
                    else
                    {
                        // 寄存器操作，长度是寄存器数量
                        registerOrCoilCount = (ushort)(data.Length / 2);
                    }
                    var request = new ModbusTcpRequest(GenerateTransactionId(), modbusAddress.StationNumber, modbusAddress.FunctionCode, modbusAddress.Address, registerOrCoilCount, data);
                    var response = await Transport.UnicastWriteMessageAsync(request);
                    return _writeResponseHandle(response);

                }
                return OperationResult.CreateFailedResult<byte[]>(result);

            }
        }

        public override async Task<OperationResult> WriteAsync(string address, bool value)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusAddress(address, DataTypeEnums.Bool, true, out var modbusAddress))
                {
                    var request = new ModbusTcpRequest(GenerateTransactionId(), modbusAddress.StationNumber, modbusAddress.FunctionCode, modbusAddress.Address, 1, new byte[2] { (byte)(value ? 0xFF : 0x00) ,0x00}) ;
                    var response = await Transport.UnicastWriteMessageAsync(request);
                    return _writeResponseHandle(response);

                }
                
                return OperationResult.CreateFailedResult<byte[]>($"地址转换失败,{address}尝试写入{value}");

            }
        }

        public override async Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusAddress(address, DataTypeEnums.Bool, true, out var modbusAddress))
                {
                    var request = new ModbusTcpRequest(GenerateTransactionId(), modbusAddress.StationNumber, modbusAddress.FunctionCode, modbusAddress.Address, (ushort)value.Length, value.ToBytes());
                    var response = await Transport.UnicastWriteMessageAsync(request);
                    return _writeResponseHandle(response);

                }
                return OperationResult.CreateFailedResult<byte[]>(result);

            }
        }

        internal OperationResult<byte[]> _writeResponseHandle(OperationResult<IDeviceReadWriteMessage> operationResult)
        {
            if (operationResult.IsSuccess)
            {
                var dataPackage = operationResult.ResultValue.ProtocolMessageFrame;
                var modbusTcpResponse = new ModbusTcpResponse(dataPackage);
                return new OperationResult<byte[]>(operationResult, modbusTcpResponse.ProtocolMessageFrame).Complete();
            }
            return OperationResult.CreateFailedResult<byte[]>(operationResult);

        }

        public ushort GenerateTransactionId()
        {
            _transactionId = (_transactionId + 1) % 255;  
            return (ushort)_transactionId;
        }

        // 批量读写核心方法
        public override async ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult<Dictionary<string, (DataTypeEnums, object)>>();
                try
                {
                    if (addresses == null || addresses.Count == 0)
                    {
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }
                    var internalAddresses = new Dictionary<string, (DataTypeEnums, object)>();
                    foreach (var kvp in addresses)
                        internalAddresses[kvp.Key] = (kvp.Value, null);
                    var addressInfos = ModbusBatchHelper.ParseModbusAddresses(internalAddresses, false);
                    if (addressInfos.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "没有有效的地址可以读取";
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }
                    var optimizedBlocks = ModbusBatchHelper.OptimizeModbusAddressBlocks(addressInfos);
                    if (optimizedBlocks.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "地址优化失败";
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }
                    var blockDataDict = new Dictionary<string, byte[]>();
                    var errors = new List<string>();
                    foreach (var block in optimizedBlocks)
                    {
                        try
                        {
                            string blockAddress = $"{block.StationNumber};{block.FunctionCode};{block.StartAddress}";
                            string blockKey = $"{block.StationNumber}_{block.FunctionCode}_{block.StartAddress}_{block.TotalLength}";
                            var readResult = await ReadAsync(blockAddress, block.TotalLength, DataTypeEnums.Byte, block.FunctionCode == 0x01 || block.FunctionCode == 0x02);
                            if (readResult.IsSuccess)
                            {
                                blockDataDict[blockKey] = readResult.ResultValue;
                                result.Requsts.AddRange(readResult.Requsts);
                                result.Responses.AddRange(readResult.Responses);
                            }
                            else
                            {
                                errors.Add($"读取块 {blockAddress} 失败: {readResult.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"读取块 {block.StationNumber};{block.FunctionCode};{block.StartAddress} 异常: {ex.Message}");
                        }
                    }
                    if (errors.Count > 0)
                    {
                        result.IsSuccess = blockDataDict.Count > 0;
                        result.Message = string.Join("; ", errors);
                    }
                    else
                    {
                        result.IsSuccess = true;
                    }
                    var extractedData = ModbusBatchHelper.ExtractDataFromModbusBlocks(blockDataDict, optimizedBlocks, addressInfos);
                    var finalResult = new Dictionary<string, (DataTypeEnums, object)>();
                    foreach (var kvp in addresses)
                    {
                        var address = kvp.Key;
                        var dataType = kvp.Value;
                        if (extractedData.TryGetValue(address, out var value))
                            finalResult[address] = (dataType, value);
                        else
                            finalResult[address] = (dataType, null);
                    }
                    result.ResultValue = finalResult;
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = $"批量读取异常: {ex.Message}";
                    result.Exception = ex;
                    result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                }
                return result.Complete();
            }
        }

        public override async ValueTask<OperationResult> BatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                var writeErrors = new List<string>();
                var successCount = 0;

                try

                {
                    if (addresses == null || addresses.Count == 0)
                        return result.Complete();
                    foreach (var address in addresses)
                    {
                        var writeResult = await WriteAsync(address.Value.Item1,address.Key,address.Value.Item2);
                        if (writeResult.IsSuccess)
                        {
                            successCount++;
                            result.Requsts.AddRange(writeResult.Requsts);
                            result.Responses.AddRange(writeResult.Responses);
                        }
                        else
                        {
                            writeErrors.Add($"写入地址 {address.Key} 失败: {writeResult.Message}");
                        }

                    }
                    if (successCount == addresses.Count)
                    {
                        result.IsSuccess = true;
                        result.Message = $"成功写入 {successCount} 个地址";
                    }
                    else if (successCount > 0)
                    {
                        result.IsSuccess = false;
                        result.Message = $"部分写入成功 ({successCount}/{addresses.Count}): {string.Join("; ", writeErrors)}";
                    }
                    else
                    {
                        result.IsSuccess = false;
                        result.Message = $"批量写入失败: {string.Join("; ", writeErrors)}";
                    }
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = $"批量写入异常: {ex.Message}";
                    result.Exception = ex;
                }
                return result.Complete();
            }
        }
    }
}
