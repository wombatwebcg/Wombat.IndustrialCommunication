using System;
using Wombat.IndustrialCommunication.Modbus;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge
{
    /// <summary>
    /// 在标准 Modbus TCP 从站之上加一个拦截点：数据陈旧时对读请求回异常码 0x04。
    /// 默认关闭，避免下游把异常码当成通讯故障反复重连。
    /// </summary>
    internal sealed class BridgeModbusTcpServer : ModbusTcpServer
    {
        public BridgeModbusTcpServer(string ip, int port)
            : base(ip, port)
        {
        }

        /// <summary>数据是否已陈旧。为空表示不拦截。</summary>
        public Func<bool> IsDataStale { get; set; }

        /// <summary>陈旧时是否对读请求返回异常码。</summary>
        public bool ReturnExceptionWhenStale { get; set; }

        protected override byte[] ProcessModbusRequest(
            byte[] request,
            ushort transactionId,
            ushort protocolId,
            byte unitId)
        {
            if (ReturnExceptionWhenStale && request != null && request.Length >= 8 && IsStale())
            {
                // 只拦数据区：1x 离散输入、3x 输入寄存器、线圈。
                // 4x 保持寄存器里是健康区与命令状态，断链时仍要能读，否则下游无从判断原因。
                var functionCode = request[7];
                if (functionCode == 0x01 || functionCode == 0x02 || functionCode == 0x04)
                {
                    return CreateExceptionResponse(request, transactionId, protocolId, unitId, 0x04);
                }
            }

            return base.ProcessModbusRequest(request, transactionId, protocolId, unitId);
        }

        private bool IsStale()
        {
            var probe = IsDataStale;
            return probe != null && probe();
        }
    }
}
