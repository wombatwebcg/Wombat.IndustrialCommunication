using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 点位列表写入结果项。
    /// </summary>
    public class DevicePointWriteResult
    {
        public string Name { get; set; }

        public string Address { get; set; }

        public DataTypeEnums DataType { get; set; }

        public int Length { get; set; }

        public bool IsSuccess { get; set; }

        public string Message { get; set; }

        public object Value { get; set; }

        public DevicePointWriteResult()
        {
            Name = string.Empty;
            Address = string.Empty;
            DataType = DataTypeEnums.None;
            Length = 1;
            Message = string.Empty;
            Value = null;
        }
    }
}
