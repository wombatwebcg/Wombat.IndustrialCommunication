using System;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.Extensions.Bluetooth;
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Factory;
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Models;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.ChannelTests
{
    public class BluetoothServerChannelTests
    {
        [Fact]
        public void IBluetoothServerChannel_ExtendsIBluetoothChannel()
        {
            Assert.True(typeof(IBluetoothChannel).IsAssignableFrom(typeof(IBluetoothServerChannel)));
        }

        [Fact]
        public void IBluetoothServerChannel_ExposesReadOnlyIsAdvertising()
        {
            var property = typeof(IBluetoothServerChannel).GetProperty("IsAdvertising");

            Assert.NotNull(property);
            Assert.Equal(typeof(bool), property.PropertyType);
            Assert.True(property.CanRead);
            Assert.False(property.CanWrite);
        }

        [Fact]
        public void Adapter_IsAdvertising_ReflectsServerChannel()
        {
            var channel = new FakeServerChannel { IsAdvertising = true };
            using var adapter = new BluetoothServerAdapter(channel);

            Assert.True(adapter.IsAdvertising);

            channel.IsAdvertising = false;
            Assert.False(adapter.IsAdvertising);
        }

        [Fact]
        public void Adapter_IsAdvertising_ReturnsFalseForPlainChannel()
        {
            var channel = new FakePlainChannel();
            using var adapter = new BluetoothServerAdapter(channel);

            Assert.False(adapter.IsAdvertising);
        }

        [Fact]
        public void Factory_NullChannel_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => BluetoothServerFactory.CreateModbusRtuServer(null));
        }

        [Fact]
        public void Factory_NullOptions_UsesDefaultSlaveId()
        {
            using var server = BluetoothServerFactory.CreateModbusRtuServer(new FakeServerChannel());

            Assert.Equal((byte)1, server.SlaveId);
        }

        [Fact]
        public void Factory_AppliesSlaveIdAndTimeoutsFromOptions()
        {
            var channel = new FakeServerChannel();
            var options = ValidOptions(slaveId: 7);

            using var server = BluetoothServerFactory.CreateModbusRtuServer(channel, options);

            Assert.Equal((byte)7, server.SlaveId);
            Assert.Equal(options.ConnectTimeout, channel.ConnectTimeout);
            Assert.Equal(options.ReceiveTimeout, channel.ReceiveTimeout);
            Assert.Equal(options.SendTimeout, channel.SendTimeout);
            Assert.Equal(options.ConnectTimeout, server.ConnectTimeout);
            Assert.Equal(options.ReceiveTimeout, server.ReceiveTimeout);
            Assert.Equal(options.SendTimeout, server.SendTimeout);
        }

        [Fact]
        public void Factory_InvalidSlaveId_Throws()
        {
            var options = ValidOptions();
            options.SlaveId = 0;

            Assert.Throws<ArgumentException>(() =>
                BluetoothServerFactory.CreateModbusRtuServer(new FakeServerChannel(), options));
        }

        [Fact]
        public void Factory_KeepsServerChannelIdentity()
        {
            var channel = new FakeServerChannel { IsAdvertising = true };
            var options = ValidOptions();

            using var server = BluetoothServerFactory.CreateModbusRtuServer(channel, options);

            // 工厂不包装通道：服务通道仍可被识别，IsAdvertising 语义保持
            Assert.IsAssignableFrom<IBluetoothServerChannel>(channel);
            Assert.True(channel.IsAdvertising);
        }

        private static BluetoothServerOptions ValidOptions(int slaveId = 1)
        {
            return new BluetoothServerOptions
            {
                ServiceId = "0000FF00-0000-1000-8000-00805F9B34FB",
                WriteCharacteristicId = "0000FF01-0000-1000-8000-00805F9B34FB",
                NotifyCharacteristicId = "0000FF02-0000-1000-8000-00805F9B34FB",
                SlaveId = slaveId,
                ConnectTimeout = TimeSpan.FromSeconds(5),
                ReceiveTimeout = TimeSpan.FromSeconds(3),
                SendTimeout = TimeSpan.FromSeconds(3)
            };
        }

        private sealed class FakeServerChannel : IBluetoothServerChannel
        {
            public bool Connected { get; private set; }

            public bool IsAdvertising { get; set; }

            public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(2);

            public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(2);

            public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(2);

            public Task<OperationResult> ConnectAsync(CancellationToken cancellationToken)
            {
                Connected = true;
                IsAdvertising = true;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public Task<OperationResult> DisconnectAsync()
            {
                Connected = false;
                IsAdvertising = false;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public Task<OperationResult> SendAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public Task<OperationResult<int>> ReceiveAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationResult.CreateFailedResult<int>("no data"));
            }

            public void Dispose()
            {
                Connected = false;
                IsAdvertising = false;
            }
        }

        private sealed class FakePlainChannel : IBluetoothChannel
        {
            public bool Connected { get; private set; }

            public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(2);

            public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(2);

            public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(2);

            public Task<OperationResult> ConnectAsync(CancellationToken cancellationToken)
            {
                Connected = true;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public Task<OperationResult> DisconnectAsync()
            {
                Connected = false;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public Task<OperationResult> SendAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public Task<OperationResult<int>> ReceiveAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationResult.CreateFailedResult<int>("no data"));
            }

            public void Dispose()
            {
                Connected = false;
            }
        }
    }
}
