using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 点位列表读取项。
    /// </summary>
    public class DevicePointReadRequest
    {
        /// <summary>
        /// 点位名称，未设置时默认使用地址。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 设备地址。
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 数据类型。
        /// </summary>
        public DataTypeEnums DataType { get; set; }

        /// <summary>
        /// 读取长度。
        /// 标量读取可保持为 1。
        /// </summary>
        public int Length { get; set; } = 1;

        /// <summary>
        /// 是否优先参与客户端批量读取。
        /// 仅兼容标量点位，字符串和数组点位会自动回退到逐点读取。
        /// </summary>
        public bool EnableBatch { get; set; } = true;

        public DevicePointReadRequest()
        {
            Name = string.Empty;
            Address = string.Empty;
            DataType = DataTypeEnums.None;
            Length = 1;
        }
    }
}
