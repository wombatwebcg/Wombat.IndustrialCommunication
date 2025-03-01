using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusTcp : DeviceDataReaderWriterBase
    {
        private AsyncLock _lock = new AsyncLock();
        private volatile int _transactionId = 0;
        public ModbusTcp(DeviceMessageTransport transport):base(transport)
        {
            DataFormat = Extensions.DataTypeExtensions.EndianFormat.CDAB;
            IsReverse = true;

        }

        public override string Version => throw new NotImplementedException();


        internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusAddress(address, out var modbusAddress))
                {
                    var readRequest = new ModbusTcpRequest(GenerateTransactionId(), modbusAddress.StationNumber,modbusAddress.FunctionCode, modbusAddress.Address, (ushort)length);
                    var response = await Transport.UnicastReadMessageAsync(readRequest);
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


        internal override async Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusAddress(address, out var modbusAddress))
                {
                    var request = new ModbusTcpRequest(GenerateTransactionId(), modbusAddress.StationNumber, modbusAddress.FunctionCode, modbusAddress.Address, (ushort)(data.Length%256),data);
                    var response = await Transport.UnicastReadMessageAsync(request);
                    if (response.IsSuccess)
                    {
                        var dataPackage = response.ResultValue.ProtocolMessageFrame;
                        var modbusTcpResponse = new ModbusTcpResponse(dataPackage);
                        return new OperationResult<byte[]>(response, modbusTcpResponse.ProtocolMessageFrame).Complete();
                    }

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
                    var request = new ModbusTcpRequest(GenerateTransactionId(), modbusAddress.StationNumber, modbusAddress.FunctionCode, modbusAddress.Address, 1, new byte[1] { (byte)(value ? 0xFF : 0x00) }) ;
                    var response = await Transport.UnicastReadMessageAsync(request);
                    if (response.IsSuccess)
                    {
                        var dataPackage = response.ResultValue.ProtocolMessageFrame;
                        var modbusTcpResponse = new ModbusTcpResponse(dataPackage);
                        return new OperationResult<byte[]>(response, modbusTcpResponse.ProtocolMessageFrame).Complete();
                    }

                }
                return OperationResult.CreateFailedResult<byte[]>(result);

            }
        }

        public override async Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusAddress(address, out var modbusAddress))
                {
                    var request = new ModbusTcpRequest(GenerateTransactionId(), modbusAddress.StationNumber, modbusAddress.FunctionCode, modbusAddress.Address, (ushort)value.Length, value.ToBytes());
                    var response = await Transport.UnicastReadMessageAsync(request);
                    if (response.IsSuccess)
                    {
                        var dataPackage = response.ResultValue.ProtocolMessageFrame;
                        var modbusTcpResponse = new ModbusTcpResponse(dataPackage);
                        return new OperationResult<byte[]>(response, modbusTcpResponse.ProtocolMessageFrame).Complete();
                    }

                }
                return OperationResult.CreateFailedResult<byte[]>(result);

            }
        }

        public ushort GenerateTransactionId()
        {
            _transactionId = (_transactionId + 1) % 256;  
            return (ushort)_transactionId;
        }
    }
}
