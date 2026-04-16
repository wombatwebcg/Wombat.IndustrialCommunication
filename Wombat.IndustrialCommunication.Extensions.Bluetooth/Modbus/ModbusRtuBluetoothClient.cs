using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Modbus;

namespace Wombat.IndustrialCommunication.Extensions.Bluetooth.Modbus
{
    public class ModbusRtuBluetoothClient : ModbusRtuClientBase, IDeviceClient, IModbusClient
    {
        private readonly BluetoothStreamAdapter _bluetoothAdapter;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private DateTime _lastReconnectAttempt = DateTime.MinValue;

        public ModbusRtuBluetoothClient(IBluetoothChannel channel)
            : base(new DeviceMessageTransport(new BluetoothStreamAdapter(channel)))
        {
            _bluetoothAdapter = (BluetoothStreamAdapter)Transport.StreamResource;
        }

        public ILogger Logger { get; set; }

        public bool EnableAutoReconnect { get; set; } = true;

        public int MaxReconnectAttempts { get; set; } = 5;

        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(2);

        public override string Version => nameof(ModbusRtuBluetoothClient);

        public TimeSpan ConnectTimeout
        {
            get => _bluetoothAdapter.ConnectTimeout;
            set => _bluetoothAdapter.ConnectTimeout = value;
        }

        public TimeSpan ReceiveTimeout
        {
            get => _bluetoothAdapter.ReceiveTimeout;
            set => _bluetoothAdapter.ReceiveTimeout = value;
        }

        public TimeSpan SendTimeout
        {
            get => _bluetoothAdapter.SendTimeout;
            set => _bluetoothAdapter.SendTimeout = value;
        }

        public bool Connected => _bluetoothAdapter.Connected;

        public int Retries
        {
            get => Transport.Retries;
            set => Transport.Retries = value;
        }

        public TimeSpan WaitToRetryMilliseconds
        {
            get => Transport.WaitToRetryMilliseconds;
            set => Transport.WaitToRetryMilliseconds = value;
        }

        public bool IsLongConnection { get; set; } = true;

        public TimeSpan ResponseInterval
        {
            get => Transport.ResponseInterval;
            set => Transport.ResponseInterval = value;
        }

        public OperationResult Connect()
        {
            return ConnectAsync().GetAwaiter().GetResult();
        }

        public async Task<OperationResult> ConnectAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Connected)
                {
                    return OperationResult.CreateSuccessResult("已连接");
                }

                return await _bluetoothAdapter.ConnectAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        public OperationResult Disconnect()
        {
            return DisconnectAsync().GetAwaiter().GetResult();
        }

        public async Task<OperationResult> DisconnectAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!Connected)
                {
                    return OperationResult.CreateSuccessResult("已断开连接");
                }

                return await _bluetoothAdapter.DisconnectAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        public OperationResult<bool> ReadCoil(byte stationNumber, ushort address)
        {
            return ReadCoilAsync(stationNumber, address).GetAwaiter().GetResult();
        }

        public OperationResult<bool[]> ReadCoils(byte stationNumber, ushort startAddress, ushort count)
        {
            return ReadCoilsAsync(stationNumber, startAddress, count).GetAwaiter().GetResult();
        }

        public OperationResult<bool> ReadDiscreteInput(byte stationNumber, ushort address)
        {
            return ReadDiscreteInputAsync(stationNumber, address).GetAwaiter().GetResult();
        }

        public OperationResult<bool[]> ReadDiscreteInputs(byte stationNumber, ushort startAddress, ushort count)
        {
            return ReadDiscreteInputsAsync(stationNumber, startAddress, count).GetAwaiter().GetResult();
        }

        public OperationResult<ushort> ReadHoldingRegister(byte stationNumber, ushort address)
        {
            return ReadHoldingRegisterAsync(stationNumber, address).GetAwaiter().GetResult();
        }

        public OperationResult<ushort[]> ReadHoldingRegisters(byte stationNumber, ushort startAddress, ushort count)
        {
            return ReadHoldingRegistersAsync(stationNumber, startAddress, count).GetAwaiter().GetResult();
        }

        public OperationResult<ushort> ReadInputRegister(byte stationNumber, ushort address)
        {
            return ReadInputRegisterAsync(stationNumber, address).GetAwaiter().GetResult();
        }

        public OperationResult<ushort[]> ReadInputRegisters(byte stationNumber, ushort startAddress, ushort count)
        {
            return ReadInputRegistersAsync(stationNumber, startAddress, count).GetAwaiter().GetResult();
        }

        public OperationResult WriteCoil(byte stationNumber, ushort address, bool value)
        {
            return WriteCoilAsync(stationNumber, address, value).GetAwaiter().GetResult();
        }

        public OperationResult WriteCoils(byte stationNumber, ushort startAddress, bool[] values)
        {
            return WriteCoilsAsync(stationNumber, startAddress, values).GetAwaiter().GetResult();
        }

        public OperationResult WriteHoldingRegister(byte stationNumber, ushort address, ushort value)
        {
            return WriteHoldingRegisterAsync(stationNumber, address, value).GetAwaiter().GetResult();
        }

        public OperationResult WriteHoldingRegisters(byte stationNumber, ushort startAddress, ushort[] values)
        {
            return WriteHoldingRegistersAsync(stationNumber, startAddress, values).GetAwaiter().GetResult();
        }

        public Task<OperationResult<bool>> ReadCoilAsync(byte stationNumber, ushort address)
        {
            var modbusAddress = stationNumber + ";1;" + address;
            return ExecuteWithConnectionAsync(async () => await ReadBooleanAsync(modbusAddress));
        }

        public Task<OperationResult<bool[]>> ReadCoilsAsync(byte stationNumber, ushort startAddress, ushort count)
        {
            var modbusAddress = stationNumber + ";1;" + startAddress;
            return ExecuteWithConnectionAsync(async () => await ReadBooleanAsync(modbusAddress, count));
        }

        public Task<OperationResult<bool>> ReadDiscreteInputAsync(byte stationNumber, ushort address)
        {
            var modbusAddress = stationNumber + ";2;" + address;
            return ExecuteWithConnectionAsync(async () => await ReadBooleanAsync(modbusAddress));
        }

        public Task<OperationResult<bool[]>> ReadDiscreteInputsAsync(byte stationNumber, ushort startAddress, ushort count)
        {
            var modbusAddress = stationNumber + ";2;" + startAddress;
            return ExecuteWithConnectionAsync(async () => await ReadBooleanAsync(modbusAddress, count));
        }

        public Task<OperationResult<ushort>> ReadHoldingRegisterAsync(byte stationNumber, ushort address)
        {
            var modbusAddress = stationNumber + ";3;" + address;
            return ExecuteWithConnectionAsync(async () => await ReadUInt16Async(modbusAddress));
        }

        public Task<OperationResult<ushort[]>> ReadHoldingRegistersAsync(byte stationNumber, ushort startAddress, ushort count)
        {
            var modbusAddress = stationNumber + ";3;" + startAddress;
            return ExecuteWithConnectionAsync(async () => await ReadUInt16Async(modbusAddress, count));
        }

        public Task<OperationResult<ushort>> ReadInputRegisterAsync(byte stationNumber, ushort address)
        {
            var modbusAddress = stationNumber + ";4;" + address;
            return ExecuteWithConnectionAsync(async () => await ReadUInt16Async(modbusAddress));
        }

        public Task<OperationResult<ushort[]>> ReadInputRegistersAsync(byte stationNumber, ushort startAddress, ushort count)
        {
            var modbusAddress = stationNumber + ";4;" + startAddress;
            return ExecuteWithConnectionAsync(async () => await ReadUInt16Async(modbusAddress, count));
        }

        public Task<OperationResult> WriteCoilAsync(byte stationNumber, ushort address, bool value)
        {
            var modbusAddress = stationNumber + ";5;" + address;
            return ExecuteWithConnectionAsync(() => WriteAsync(modbusAddress, value));
        }

        public Task<OperationResult> WriteCoilsAsync(byte stationNumber, ushort startAddress, bool[] values)
        {
            var modbusAddress = stationNumber + ";15;" + startAddress;
            return ExecuteWithConnectionAsync(() => WriteAsync(modbusAddress, values));
        }

        public Task<OperationResult> WriteHoldingRegisterAsync(byte stationNumber, ushort address, ushort value)
        {
            var modbusAddress = stationNumber + ";6;" + address;
            return ExecuteWithConnectionAsync(() => WriteAsync(modbusAddress, value));
        }

        public Task<OperationResult> WriteHoldingRegistersAsync(byte stationNumber, ushort startAddress, ushort[] values)
        {
            var modbusAddress = stationNumber + ";16;" + startAddress;
            return ExecuteWithConnectionAsync(() => WriteAsync(modbusAddress, values));
        }

        private async Task<OperationResult> EnsureConnectedAsync()
        {
            if (Connected)
            {
                return OperationResult.CreateSuccessResult();
            }

            if (!EnableAutoReconnect)
            {
                return OperationResult.CreateFailedResult("蓝牙未连接");
            }

            if (DateTime.Now - _lastReconnectAttempt < ReconnectDelay)
            {
                return OperationResult.CreateFailedResult("重连间隔未到");
            }

            _lastReconnectAttempt = DateTime.Now;

            for (var i = 0; i < MaxReconnectAttempts; i++)
            {
                var result = await ConnectAsync().ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    return result;
                }

                await Task.Delay(ReconnectDelay).ConfigureAwait(false);
            }

            return OperationResult.CreateFailedResult("自动重连失败");
        }

        private async Task<OperationResult<T>> ExecuteWithConnectionAsync<T>(Func<Task<OperationResult<T>>> action)
        {
            var shouldDisconnect = !IsLongConnection;
            if (!Connected)
            {
                var connectResult = shouldDisconnect ? await ConnectAsync().ConfigureAwait(false) : await EnsureConnectedAsync().ConfigureAwait(false);
                if (!connectResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<T>(connectResult);
                }
            }

            try
            {
                return await action().ConfigureAwait(false);
            }
            finally
            {
                if (shouldDisconnect && Connected)
                {
                    await DisconnectAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task<OperationResult> ExecuteWithConnectionAsync(Func<Task<OperationResult>> action)
        {
            var shouldDisconnect = !IsLongConnection;
            if (!Connected)
            {
                var connectResult = shouldDisconnect ? await ConnectAsync().ConfigureAwait(false) : await EnsureConnectedAsync().ConfigureAwait(false);
                if (!connectResult.IsSuccess)
                {
                    return connectResult;
                }
            }

            try
            {
                return await action().ConfigureAwait(false);
            }
            finally
            {
                if (shouldDisconnect && Connected)
                {
                    await DisconnectAsync().ConfigureAwait(false);
                }
            }
        }

        protected new virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disconnect();
            }

            base.Dispose(disposing);
        }

        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
