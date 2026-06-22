using System;
using System.Collections.Generic;
using System.Linq;

namespace Wombat.IndustrialCommunication.PLC
{
    public class S7ReadResponse : IDeviceReadWriteMessage
    {
        public sealed class ItemResult
        {
            public SiemensAddress Item { get; set; }

            public bool IsSuccess { get; set; }

            public byte ReturnCode { get; set; }

            public byte TransportSize { get; set; }

            public byte[] Data { get; set; }

            public string Message { get; set; }
        }

        public S7ReadResponse(byte[] data)
        {
            ProtocolMessageFrame = data;
        }

        public byte[] ProtocolMessageFrame { get; set; }

        public int ProtocolDataNumber { get; set; }

        public int RegisterCount { get; set; }

        public string RegisterAddress { get; set; }

        public int ProtocolResponseLength { get; set; } = SiemensConstant.InitHeadLength;

        public List<ItemResult> Items { get; } = new List<ItemResult>();

        public bool HasProtocolFailure { get; set; }

        public string ProtocolFailureMessage { get; set; }

        public void Initialize(byte[] frame)
        {
        }

        public static OperationResult<S7ReadResponse> Parse(byte[] dataPackage, IReadOnlyList<SiemensAddress> items)
        {
            var protocolResult = new OperationResult<S7ReadResponse>();
            var parsed = new S7ReadResponse(dataPackage)
            {
                RegisterAddress = items == null ? null : string.Join(",", items.Select(t => t.OriginalAddress ?? t.Address)),
                RegisterCount = items?.Count ?? 0
            };

            try
            {
                if (dataPackage == null || dataPackage.Length < 25)
                {
                    return OperationResult.CreateFailedResult<S7ReadResponse>("批量随机读响应长度不足");
                }

                int cotpTotalLength = 1 + dataPackage[4];
                int s7Offset = 4 + cotpTotalLength;
                if (s7Offset + 12 > dataPackage.Length)
                {
                    return OperationResult.CreateFailedResult<S7ReadResponse>("批量随机读S7响应头长度不足");
                }

                int parameterLength = (dataPackage[s7Offset + 6] << 8) | dataPackage[s7Offset + 7];
                int dataLength = (dataPackage[s7Offset + 8] << 8) | dataPackage[s7Offset + 9];
                int parameterOffset = s7Offset + 12;
                int dataOffset = parameterOffset + parameterLength;

                if (parameterOffset + parameterLength > dataPackage.Length || dataOffset + dataLength > dataPackage.Length)
                {
                    return OperationResult.CreateFailedResult<S7ReadResponse>("批量随机读响应参数或数据长度无效");
                }

                if (parameterLength < 2 || dataPackage[parameterOffset] != 0x04)
                {
                    return OperationResult.CreateFailedResult<S7ReadResponse>("批量随机读响应功能码异常");
                }

                int itemCount = dataPackage[parameterOffset + 1];
                if (items == null || itemCount != items.Count)
                {
                    return OperationResult.CreateFailedResult<S7ReadResponse>("批量随机读响应项数与请求不一致");
                }

                int cursor = dataOffset;
                foreach (var item in items)
                {
                    if (cursor + 4 > dataPackage.Length)
                    {
                        return OperationResult.CreateFailedResult<S7ReadResponse>($"地址 {item.OriginalAddress ?? item.Address} 响应长度不足");
                    }

                    byte returnCode = dataPackage[cursor];
                    byte transportSize = dataPackage[cursor + 1];
                    int payloadBitLength = (dataPackage[cursor + 2] << 8) | dataPackage[cursor + 3];
                    cursor += 4;

                    var itemResult = new ItemResult
                    {
                        Item = item,
                        ReturnCode = returnCode,
                        TransportSize = transportSize,
                        IsSuccess = returnCode == 0xFF
                    };

                    int payloadByteLength = (int)Math.Ceiling(payloadBitLength / 8.0);
                    int expectedLength = item.RequestLength;

                    if (returnCode == 0x0A || returnCode == 0x05)
                    {
                        itemResult.Message = $"读取 {item.OriginalAddress ?? item.Address} 失败，请确认地址是否存在";
                    }
                    else if (returnCode != 0xFF)
                    {
                        itemResult.Message = $"读取 {item.OriginalAddress ?? item.Address} 失败，异常状态:{returnCode}";
                    }

                    if (itemResult.IsSuccess)
                    {
                        if (cursor + payloadByteLength > dataPackage.Length || payloadByteLength < expectedLength)
                        {
                            return OperationResult.CreateFailedResult<S7ReadResponse>($"{item.OriginalAddress ?? item.Address} 读取预期长度与返回数据长度不一致");
                        }

                        itemResult.Data = new byte[expectedLength];
                        Buffer.BlockCopy(dataPackage, cursor, itemResult.Data, 0, expectedLength);
                    }

                    cursor += payloadByteLength == 1 ? 2 : payloadByteLength;
                    parsed.Items.Add(itemResult);
                }

                protocolResult.ResultValue = parsed;
                return protocolResult.Complete();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<S7ReadResponse>($"批量随机读响应解析异常: {ex.Message}");
            }
        }
    }
}
