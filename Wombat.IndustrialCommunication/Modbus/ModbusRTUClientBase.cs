using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusRTUClientBase : DeviceDataReaderWriterBase
    {
        private AsyncLock _lock = new AsyncLock();
        private volatile int _transactionId = 0;
        public ModbusRTUClientBase(DeviceMessageTransport transport):base(transport)
        {
            DataFormat = Extensions.DataTypeExtensions.EndianFormat.ABCD;
            IsReverse = true;

        }

        public override string Version => nameof(ModbusRTUClientBase);


        internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusAddress(address, out var modbusAddress))
                {
                    var request = new ModbusRTURequest(modbusAddress.StationNumber,modbusAddress.FunctionCode, modbusAddress.Address, (ushort)length);
                    var response = await Transport.UnicastReadMessageAsync(request);
                    if (response.IsSuccess)
                    {
                        var dataPackage = response.ResultValue.ProtocolMessageFrame;
                        var modbusTcpResponse = new ModbusRTUResponse(dataPackage);
                        if (!CRC16Helper.ValidateCRC(dataPackage))
                            throw new InvalidOperationException("CRC check failed");
                        // 处理异常响应（功能码最高位为1）
                        if ((modbusTcpResponse.FunctionCode & 0x80) != 0)
                        {
                            result.IsSuccess = false;
                            result.ErrorCode = dataPackage[2];
                            result.Message = $"ModbusRTU回复错误码:{result.ErrorCode}";
                            return OperationResult.CreateFailedResult<byte[]>(result);
                        }

                        return new OperationResult<byte[]>(response, modbusTcpResponse.Data).Complete();
                    }
                    else
                    {
                        return OperationResult.CreateFailedResult<byte[]>(response);

                    }

                }
                return OperationResult.CreateFailedResult<byte[]>(result);

            }
        }


        internal override async Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusAddress(address, out var modbusAddress))
                {
                    var request = new ModbusRTURequest(modbusAddress.StationNumber, modbusAddress.FunctionCode, modbusAddress.Address, (ushort)(data.Length%256),data);
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
                if (ModbusAddressParser.TryParseModbusAddress(address, out var modbusAddress))
                {
                    var request = new ModbusRTURequest(modbusAddress.StationNumber, modbusAddress.FunctionCode, modbusAddress.Address, 1, BitConverter.GetBytes(value)) ;
                    var response = await Transport.UnicastWriteMessageAsync(request);
                    return _writeResponseHandle(response);

                }
                return OperationResult.CreateFailedResult<byte[]>(result);

            }
        }

        public  override async Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusAddress(address, out var modbusAddress))
                {
                    var request = new ModbusRTURequest(modbusAddress.StationNumber, modbusAddress.FunctionCode, modbusAddress.Address, (ushort)value.Length, value.ToBytes());
                    var response = await Transport.UnicastWriteMessageAsync(request);
                    return _writeResponseHandle(response);

                }
                return OperationResult.CreateFailedResult<byte[]>(result);

            }
        }

        internal virtual  OperationResult<byte[]> _writeResponseHandle(OperationResult<IDeviceReadWriteMessage> operationResult)
        {
            if (operationResult.IsSuccess)
            {
                var dataPackage = operationResult.ResultValue.ProtocolMessageFrame;
                var modbusTcpResponse = new ModbusRTUResponse(dataPackage);
                if (!CRC16Helper.ValidateCRC(dataPackage))
                    throw new InvalidOperationException("CRC check failed");
                // 处理异常响应（功能码最高位为1）
                if ((modbusTcpResponse.FunctionCode & 0x80) != 0)
                {
                    operationResult.IsSuccess = false;
                    operationResult.ErrorCode = dataPackage[2];
                    operationResult.Message = $"ModbusRTU回复错误码:{operationResult.ErrorCode}";
                    return OperationResult.CreateFailedResult<byte[]>(operationResult);
                }

                return new OperationResult<byte[]>(operationResult, modbusTcpResponse.ProtocolMessageFrame).Complete();
            }
            return OperationResult.CreateFailedResult<byte[]>(operationResult);

        }


    }
}
