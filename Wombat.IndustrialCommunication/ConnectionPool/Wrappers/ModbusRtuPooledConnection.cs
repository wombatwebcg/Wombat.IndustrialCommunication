using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.ConnectionPool.Wrappers
{
    public sealed class ModbusRtuPooledConnection : BasePooledDeviceClientConnection
    {
        private readonly string _probeAddress;
        private readonly DataTypeEnums _probeDataType;
        private readonly int _probeLength;

        public ModbusRtuPooledConnection(ConnectionIdentity identity, ModbusRtuClient client, string probeAddress = null, DataTypeEnums probeDataType = DataTypeEnums.UInt16, int probeLength = 1)
            : base(identity, client)
        {
            _probeAddress = probeAddress;
            _probeDataType = probeDataType;
            _probeLength = probeLength <= 0 ? 1 : probeLength;
        }
    }
}
