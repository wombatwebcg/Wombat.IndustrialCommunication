using System;
using System.Collections.Generic;
using System.Linq;
using Wombat.Extensions.DataTypeExtensions;


namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS写入请求消息类
    /// </summary>
    public class FinsWriteRequest : IDeviceReadWriteMessage
    {
        /// <summary>
        /// 协议消息帧
        /// </summary>
        public byte[] ProtocolMessageFrame { get; private set; }

        /// <summary>
        /// 地址信息
        /// </summary>
        public FinsAddress Address { get; private set; }

        /// <summary>
        /// 写入数据
        /// </summary>
        public byte[] WriteData { get; private set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public DataTypeEnums DataType { get; private set; }

        /// <summary>
        /// 服务ID
        /// </summary>
        public byte ServiceId { get; private set; }

        /// <summary>
        /// 寄存器数量
        /// </summary>
        public int RegisterCount { get; set; }

        /// <summary>
        /// 寄存器地址
        /// </summary>
        public string RegisterAddress { get; set; }

        /// <summary>
        /// 协议响应长度
        /// </summary>
        public int ProtocolResponseLength { get; set; }

        /// <summary>
        /// 协议数据编号
        /// </summary>
        public int ProtocolDataNumber { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="address">地址字符串</param>
        /// <param name="value">写入值</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="serviceId">服务ID</param>
        public FinsWriteRequest(string address, object value, DataTypeEnums dataType = DataTypeEnums.UInt16, byte serviceId = 0x00)
        {
            Address = new FinsAddress(address, dataType);
            DataType = dataType;
            ServiceId = serviceId;
            WriteData = ConvertValueToBytes(value, dataType);
            BuildMessage();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="finsAddress">FINS地址对象</param>
        /// <param name="value">写入值</param>
        /// <param name="serviceId">服务ID</param>
        public FinsWriteRequest(FinsAddress finsAddress, object value, byte serviceId = 0x00)
        {
            Address = finsAddress ?? throw new ArgumentNullException(nameof(finsAddress));
            DataType = finsAddress.DataType;
            ServiceId = serviceId;
            WriteData = ConvertValueToBytes(value, DataType);
            BuildMessage();
        }

        /// <summary>
        /// 构造函数（直接使用字节数组）
        /// </summary>
        /// <param name="address">地址字符串</param>
        /// <param name="data">写入数据</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="serviceId">服务ID</param>
        public FinsWriteRequest(string address, byte[] data, DataTypeEnums dataType = DataTypeEnums.UInt16, byte serviceId = 0x00)
        {
            Address = new FinsAddress(address, dataType);
            DataType = dataType;
            ServiceId = serviceId;
            WriteData = data ?? throw new ArgumentNullException(nameof(data));
            BuildMessage();
        }

        public void Initialize(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                ProtocolMessageFrame = data;
            }
        }

        /// <summary>
        /// 构建消息
        /// </summary>
        private void BuildMessage()
        {
            ValidateParameters();
            ProtocolMessageFrame = BuildWriteCommand();
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        private void ValidateParameters()
        {
            if (Address == null)
                throw new ArgumentNullException(nameof(Address));

            if (WriteData == null || WriteData.Length == 0)
                throw new ArgumentException("写入数据不能为空", nameof(WriteData));

            if (WriteData.Length > 1998)
                throw new ArgumentException("单次写入数据长度不能超过1998字节", nameof(WriteData));

            // 位操作时，数据长度限制为1字节
            if (Address.IsBit && WriteData.Length > 1)
                throw new ArgumentException("位操作时写入数据长度只能为1字节", nameof(WriteData));
        }

        /// <summary>
        /// 转换值为字节数组
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="dataType">数据类型</param>
        /// <returns>字节数组</returns>
        private byte[] ConvertValueToBytes(object value, DataTypeEnums dataType)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return FinsCommonMethods.ConvertToBytes(value, dataType);
        }

        /// <summary>
        /// 构建写入命令
        /// </summary>
        /// <returns>命令字节数组</returns>
        private byte[] BuildWriteCommand()
        {
            var command = new List<byte>();

            // 添加FINS头部 (10字节)
            var header = FinsCommonMethods.BuildFinsHeader(
                icf: 0x80,      // 命令
                rsv: 0x00,      // 保留
                gct: 0x02,      // 网关计数
                dna: 0x00,      // 目标网络地址
                da1: 0x00,      // 目标节点号
                da2: 0x00,      // 目标单元地址
                sna: 0x00,      // 源网络地址
                sa1: 0x00,      // 源节点号
                sa2: 0x00,      // 源单元地址
                sid: ServiceId  // 服务ID
            );
            command.AddRange(header);

            // 添加命令码 (2字节)
            command.Add(FinsConstants.CommandCodes.MEMORY_AREA_WRITE); // MRC: 主请求码
            command.Add(0x02); // SRC: 子请求码

            // 添加内存区域码 (1字节)
            command.Add(Address.GetMemoryAreaCode());

            // 添加地址 (3字节)
            var addressBytes = Address.GetAddressBytes();
            command.AddRange(addressBytes);

            // 添加写入长度 (2字节)
            int writeLength;
            if (Address.IsBit)
            {
                // 位操作：长度固定为1
                writeLength = 1;
            }
            else
            {
                // 字操作：根据数据类型计算字长度
                switch (DataType)
                {
                    case DataTypeEnums.Byte:
                        writeLength = WriteData.Length;
                        break;
                    case DataTypeEnums.UInt16:
                    case DataTypeEnums.Int16:
                        writeLength = WriteData.Length / 2;
                        break;
                    case DataTypeEnums.UInt32:
                    case DataTypeEnums.Int32:
                    case DataTypeEnums.Float:
                        writeLength = WriteData.Length / 4;
                        break;
                    case DataTypeEnums.Double:
                        writeLength = WriteData.Length / 8;
                        break;
                    case DataTypeEnums.String:
                        writeLength = WriteData.Length;
                        break;
                    default:
                        writeLength = WriteData.Length / 2; // 默认按字处理
                        break;
                }
            }

            command.Add((byte)(writeLength >> 8));
            command.Add((byte)(writeLength & 0xFF));

            // 添加写入数据
            command.AddRange(WriteData);

            return command.ToArray();
        }

        /// <summary>
        /// 创建批量写入请求
        /// </summary>
        /// <param name="writeItems">写入项列表</param>
        /// <param name="serviceId">服务ID</param>
        /// <returns>批量写入请求</returns>
        public static FinsWriteRequest CreateBatchWriteRequest(List<FinsWriteItem> writeItems, byte serviceId = 0x00)
        {
            if (writeItems == null || writeItems.Count == 0)
                throw new ArgumentException("写入项列表不能为空", nameof(writeItems));

            if (writeItems.Count > 999)
                throw new ArgumentException("批量写入项数量不能超过999个", nameof(writeItems));

            // 创建批量写入请求
            var batchRequest = new FinsWriteRequest(writeItems[0].Address, writeItems[0].Data, writeItems[0].DataType, serviceId);
            batchRequest.BuildBatchWriteCommand(writeItems);
            return batchRequest;
        }

        /// <summary>
        /// 构建批量写入命令
        /// </summary>
        /// <param name="writeItems">写入项列表</param>
        private void BuildBatchWriteCommand(List<FinsWriteItem> writeItems)
        {
            var command = new List<byte>();

            // 添加FINS头部 (10字节)
            var header = FinsCommonMethods.BuildFinsHeader(
                icf: 0x80,      // 命令
                rsv: 0x00,      // 保留
                gct: 0x02,      // 网关计数
                dna: 0x00,      // 目标网络地址
                da1: 0x00,      // 目标节点号
                da2: 0x00,      // 目标单元地址
                sna: 0x00,      // 源网络地址
                sa1: 0x00,      // 源节点号
                sa2: 0x00,      // 源单元地址
                sid: ServiceId  // 服务ID
            );
            command.AddRange(header);

            // 添加命令码 (2字节) - 使用内存区域传输命令进行批量写入
            command.Add(FinsConstants.CommandCodes.MEMORY_AREA_TRANSFER); // MRC: 主请求码
            command.Add(0x05); // SRC: 子请求码

            // 添加写入项数量 (1字节)
            command.Add((byte)writeItems.Count);

            // 添加每个写入项的信息
            foreach (var item in writeItems)
            {
                var address = new FinsAddress(item.Address, item.DataType);
                
                // 内存区域码 (1字节)
                command.Add(address.GetMemoryAreaCode());

                // 地址 (3字节)
                var addressBytes = address.GetAddressBytes();
                command.AddRange(addressBytes);

                // 写入长度 (2字节)
                int writeLength;
                if (address.IsBit)
                {
                    writeLength = 1;
                }
                else
                {
                    switch (item.DataType)
                    {
                        case DataTypeEnums.UInt16:
                        case DataTypeEnums.Int16:
                            writeLength = 1; // FINS中字长度为1
                            break;
                        case DataTypeEnums.UInt32:
                        case DataTypeEnums.Int32:
                        case DataTypeEnums.Float:
                            writeLength = 2; // FINS中双字长度为2
                            break;
                        default:
                            writeLength = item.Data.Length / 2;
                            break;
                    }
                }

                command.Add((byte)(writeLength >> 8));
                command.Add((byte)(writeLength & 0xFF));

                // 写入数据
                command.AddRange(item.Data);
            }

            ProtocolMessageFrame = command.ToArray();
        }

        /// <summary>
        /// 创建简单写入请求
        /// </summary>
        /// <param name="address">地址字符串</param>
        /// <param name="value">写入值</param>
        /// <param name="dataType">数据类型</param>
        /// <returns>写入请求</returns>
        public static FinsWriteRequest Create(string address, object value, DataTypeEnums dataType = DataTypeEnums.UInt16)
        {
            return new FinsWriteRequest(address, value, dataType);
        }

        /// <summary>
        /// 创建位写入请求
        /// </summary>
        /// <param name="address">位地址</param>
        /// <param name="value">布尔值</param>
        /// <returns>写入请求</returns>
        public static FinsWriteRequest CreateBitWrite(string address, bool value)
        {
            return new FinsWriteRequest(address, value, DataTypeEnums.Bool);
        }

        /// <summary>
        /// 创建字写入请求
        /// </summary>
        /// <param name="address">字地址</param>
        /// <param name="value">16位整数值</param>
        /// <returns>写入请求</returns>
        public static FinsWriteRequest CreateWordWrite(string address, ushort value)
        {
            return new FinsWriteRequest(address, value, DataTypeEnums.UInt16);
        }

        /// <summary>
        /// 创建双字写入请求
        /// </summary>
        /// <param name="address">双字地址</param>
        /// <param name="value">32位整数值</param>
        /// <returns>写入请求</returns>
        public static FinsWriteRequest CreateDWordWrite(string address, uint value)
        {
            return new FinsWriteRequest(address, value, DataTypeEnums.UInt32);
        }

        /// <summary>
        /// 创建浮点数写入请求
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">浮点数值</param>
        /// <returns>写入请求</returns>
        public static FinsWriteRequest CreateFloatWrite(string address, float value)
        {
            return new FinsWriteRequest(address, value, DataTypeEnums.Float);
        }

        /// <summary>
        /// 创建字符串写入请求
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">字符串值</param>
        /// <param name="length">字符串长度</param>
        /// <returns>写入请求</returns>
        public static FinsWriteRequest CreateStringWrite(string address, string value, int length = -1)
        {
            if (length > 0 && value.Length > length)
            {
                value = value.Substring(0, length);
            }
            else if (length > 0 && value.Length < length)
            {
                value = value.PadRight(length, '\0');
            }

            return new FinsWriteRequest(address, value, DataTypeEnums.String);
        }

        /// <summary>
        /// 获取期望的响应长度
        /// </summary>
        /// <returns>响应长度</returns>
        public int GetExpectedResponseLength()
        {
            // FINS写入响应只包含响应头 (12字节)
            return 12;
        }

        /// <summary>
        /// 获取消息描述
        /// </summary>
        /// <returns>消息描述</returns>
        public override string ToString()
        {
            return $"FINS写入请求 - 地址: {Address}, 数据长度: {WriteData?.Length ?? 0}字节, 数据类型: {DataType}";
        }
    }

    /// <summary>
    /// FINS写入项
    /// </summary>
    public class FinsWriteItem
    {
        /// <summary>
        /// 地址字符串
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 写入数据
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public DataTypeEnums DataType { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <param name="dataType">数据类型</param>
        public FinsWriteItem(string address, object value, DataTypeEnums dataType)
        {
            Address = address;
            DataType = dataType;
            Data = FinsCommonMethods.ConvertToBytes(value, dataType);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="data">数据</param>
        /// <param name="dataType">数据类型</param>
        public FinsWriteItem(string address, byte[] data, DataTypeEnums dataType)
        {
            Address = address;
            Data = data;
            DataType = dataType;
        }
    }


}