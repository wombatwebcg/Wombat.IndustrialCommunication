using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 点位列表写入项。
    /// </summary>
    public class DevicePointWriteRequest
    {
        public string Name { get; set; }

        public string Address { get; set; }

        public DataTypeEnums DataType { get; set; }

        /// <summary>
        /// 数组或字符串写入时可用于表达期望长度。
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// 是否优先参与客户端批量写入。
        /// 仅兼容标量点位，字符串和数组点位会自动回退到逐点写入。
        /// </summary>
        public bool EnableBatch { get; set; }

        public object Value { get; set; }

        public DevicePointWriteRequest()
        {
            Name = string.Empty;
            Address = string.Empty;
            DataType = DataTypeEnums.None;
            Length = 1;
            EnableBatch = true;
            Value = null;
        }
    }
}
