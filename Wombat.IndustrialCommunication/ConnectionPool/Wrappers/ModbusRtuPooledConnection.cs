using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.Extensions.DataTypeExtensions;
using System.Threading.Tasks;

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

        protected override async Task<OperationResult> ProbeCoreAsync()
        {
            var client = (ModbusRtuClient)Client;
            if (string.IsNullOrWhiteSpace(_probeAddress))
            {
                return await base.ProbeCoreAsync().ConfigureAwait(false);
            }

            var result = _probeLength > 1
                ? await client.ReadAsync(_probeDataType, _probeAddress, _probeLength).ConfigureAwait(false)
                : await client.ReadAsync(_probeDataType, _probeAddress).ConfigureAwait(false);
            return result.IsSuccess
                ? OperationResult.CreateSuccessResult("Modbus RTU 探活成功")
                : OperationResult.CreateFailedResult(result);
        }
    }
}
