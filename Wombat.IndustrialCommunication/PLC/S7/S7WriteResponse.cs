using System;
using System.Collections.Generic;
using System.Linq;

namespace Wombat.IndustrialCommunication.PLC
{
    public class S7WriteResponse : IDeviceReadWriteMessage
    {
        public sealed class ItemResult
        {
            public SiemensAddress Item { get; set; }

            public bool IsSuccess { get; set; }

            public byte ReturnCode { get; set; }

            public string Message { get; set; }
        }

        public S7WriteResponse(byte[] data)
        {
            ProtocolMessageFrame = data;
        }

        public byte[] ProtocolMessageFrame { get; set; }

        public int ProtocolDataNumber { get; set; }

        public int RegisterCount { get; set; }

        public string RegisterAddress { get; set; }

        public int ProtocolResponseLength { get; set; } = SiemensConstant.InitHeadLength;

        public List<ItemResult> Items { get; } = new List<ItemResult>();

        public int SuccessCount { get; set; }

        public int FailureCount => Items.Count - SuccessCount;

        public void Initialize(byte[] frame)
        {
        }

        public static OperationResult<S7WriteResponse> Parse(byte[] dataPackage, IReadOnlyList<SiemensAddress> items)
        {
            try
            {
                if (dataPackage == null || dataPackage.Length < 23)
                {
                    return OperationResult.CreateFailedResult<S7WriteResponse>("批量随机写响应长度不足");
                }

                int cotpTotalLength = 1 + dataPackage[4];
                int s7Offset = 4 + cotpTotalLength;
                if (s7Offset + 12 > dataPackage.Length)
                {
                    return OperationResult.CreateFailedResult<S7WriteResponse>("批量随机写S7响应头长度不足");
                }

                int parameterLength = (dataPackage[s7Offset + 6] << 8) | dataPackage[s7Offset + 7];
                int dataLength = (dataPackage[s7Offset + 8] << 8) | dataPackage[s7Offset + 9];
                int parameterOffset = s7Offset + 12;
                int dataOffset = parameterOffset + parameterLength;

                if (parameterOffset + parameterLength > dataPackage.Length || dataOffset + dataLength > dataPackage.Length)
                {
                    return OperationResult.CreateFailedResult<S7WriteResponse>("批量随机写响应参数或数据长度无效");
                }

                if (parameterLength < 2 || dataPackage[parameterOffset] != 0x05)
                {
                    return OperationResult.CreateFailedResult<S7WriteResponse>("批量随机写响应功能码异常");
                }

                int itemCount = dataPackage[parameterOffset + 1];
                if (items == null || itemCount != items.Count || dataLength < itemCount)
                {
                    return OperationResult.CreateFailedResult<S7WriteResponse>("批量随机写响应项数与请求不一致");
                }

                var parsed = new S7WriteResponse(dataPackage)
                {
                    RegisterAddress = string.Join(",", items.Select(t => t.OriginalAddress ?? t.Address)),
                    RegisterCount = items.Count
                };

                for (int i = 0; i < items.Count; i++)
                {
                    byte returnCode = dataPackage[dataOffset + i];
                    var itemResult = new ItemResult
                    {
                        Item = items[i],
                        ReturnCode = returnCode,
                        IsSuccess = returnCode == 0xFF,
                        Message = BuildItemMessage(items[i], returnCode)
                    };

                    if (itemResult.IsSuccess)
                    {
                        parsed.SuccessCount++;
                    }

                    parsed.Items.Add(itemResult);
                }

                return OperationResult.CreateSuccessResult(parsed);
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<S7WriteResponse>($"批量随机写响应解析异常: {ex.Message}");
            }
        }

        private static string BuildItemMessage(SiemensAddress item, byte returnCode)
        {
            if (returnCode == 0xFF)
            {
                return null;
            }

            if (returnCode == 0x0A || returnCode == 0x05)
            {
                return $"写入 {item.OriginalAddress ?? item.Address} 失败，请确认地址是否存在";
            }

            return $"写入 {item.OriginalAddress ?? item.Address} 失败，异常状态:{returnCode}";
        }
    }
}
