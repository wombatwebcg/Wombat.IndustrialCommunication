using System;
using System.Collections.Generic;
using System.Linq;
using Wombat.Extensions.DataTypeExtensions;


namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS读取请求消息类
    /// </summary>
    public class FinsReadRequest : IDeviceReadWriteMessage
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
        /// 读取长度
        /// </summary>
        public int Length { get; private set; }

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
        /// <param name="length">读取长度</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="serviceId">服务ID</param>
        public FinsReadRequest(string address, int length, DataTypeEnums dataType = DataTypeEnums.UInt16, byte serviceId = 0x00)
        {
            Address = new FinsAddress(address, dataType);
            Length = length;
            DataType = dataType;
            ServiceId = serviceId;
            BuildMessage();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="finsAddress">FINS地址对象</param>
        /// <param name="length">读取长度</param>
        /// <param name="serviceId">服务ID</param>
        public FinsReadRequest(FinsAddress finsAddress, int length, byte serviceId = 0x00)
        {
            Address = finsAddress ?? throw new ArgumentNullException(nameof(finsAddress));
            Length = length;
            DataType = finsAddress.DataType;
            ServiceId = serviceId;
            BuildMessage();
        }

        /// <summary>
        /// 构建消息
        /// </summary>
        private void BuildMessage()
        {
            ValidateParameters();
            ProtocolMessageFrame = BuildReadCommand();
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        private void ValidateParameters()
        {
            if (Address == null)
                throw new ArgumentNullException(nameof(Address));

            if (Length <= 0)
                throw new ArgumentException("读取长度必须大于0", nameof(Length));

            if (Length > 999)
                throw new ArgumentException("单次读取长度不能超过999字", nameof(Length));

            // 位操作时，长度限制为1
            if (Address.IsBit && Length > 1)
                throw new ArgumentException("位操作时读取长度只能为1", nameof(Length));
        }

        /// <summary>
        /// 构建读取命令
        /// </summary>
        /// <returns>命令字节数组</returns>
        private byte[] BuildReadCommand()
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
            command.Add(FinsConstants.CommandCodes.MEMORY_AREA_READ); // MRC: 主请求码
            command.Add(0x01); // SRC: 子请求码

            // 添加内存区域码 (1字节)
            command.Add(Address.GetMemoryAreaCode());

            // 添加地址 (3字节)
            var addressBytes = Address.GetAddressBytes();
            command.AddRange(addressBytes);

            // 添加读取长度 (2字节)
            if (Address.IsBit)
            {
                // 位操作：长度固定为1
                command.Add(0x00);
                command.Add(0x01);
            }
            else
            {
                // 字操作：按实际长度
                command.Add((byte)(Length >> 8));
                command.Add((byte)(Length & 0xFF));
            }

            return command.ToArray();
        }

        /// <summary>
        /// 获取批量读取命令
        /// </summary>
        /// <param name="addresses">地址列表</param>
        /// <param name="serviceId">服务ID</param>
        /// <returns>批量读取请求</returns>
        public static FinsReadRequest CreateBatchReadRequest(List<FinsAddress> addresses, byte serviceId = 0x00)
        {
            if (addresses == null || addresses.Count == 0)
                throw new ArgumentException("地址列表不能为空", nameof(addresses));

            if (addresses.Count > 999)
                throw new ArgumentException("批量读取地址数量不能超过999个", nameof(addresses));

            // 创建批量读取请求
            var batchRequest = new FinsReadRequest(addresses[0], 1, serviceId);
            batchRequest.BuildBatchReadCommand(addresses);
            return batchRequest;
        }

        /// <summary>
        /// 构建批量读取命令
        /// </summary>
        /// <param name="addresses">地址列表</param>
        private void BuildBatchReadCommand(List<FinsAddress> addresses)
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
            command.Add(FinsConstants.CommandCodes.MULTIPLE_MEMORY_AREA_READ); // MRC: 主请求码
            command.Add(0x04); // SRC: 子请求码

            // 添加地址数量 (1字节)
            command.Add((byte)addresses.Count);

            // 添加每个地址的信息
            foreach (var address in addresses)
            {
                // 内存区域码 (1字节)
                command.Add(address.GetMemoryAreaCode());

                // 地址 (3字节)
                var addressBytes = address.GetAddressBytes();
                command.AddRange(addressBytes);

                // 读取长度 (2字节)
                int readLength = FinsCommonMethods.GetDataTypeLength(address.DataType);
                if (address.IsBit)
                {
                    readLength = 1;
                }
                else if (address.DataType == DataTypeEnums.UInt16 || address.DataType == DataTypeEnums.Int16)
                {
                    readLength = 1; // FINS中字长度为1
                }
                else if (address.DataType == DataTypeEnums.UInt32 || address.DataType == DataTypeEnums.Int32 || address.DataType == DataTypeEnums.Float)
                {
                    readLength = 2; // FINS中双字长度为2
                }

                command.Add((byte)(readLength >> 8));
                command.Add((byte)(readLength & 0xFF));
            }

            ProtocolMessageFrame = command.ToArray();
        }

        /// <summary>
        /// 获取期望的响应长度
        /// </summary>
        /// <returns>响应长度</returns>
        public int GetExpectedResponseLength()
        {
            // FINS响应头 (12字节) + 数据长度
            int headerLength = 12;
            int dataLength;

            if (Address.IsBit)
            {
                // 位数据：1字节
                dataLength = 1;
            }
            else
            {
                // 字数据：根据数据类型计算
                switch (DataType)
                {
                    case DataTypeEnums.Byte:
                        dataLength = Length;
                        break;
                    case DataTypeEnums.UInt16:
                    case DataTypeEnums.Int16:
                        dataLength = Length * 2;
                        break;
                    case DataTypeEnums.UInt32:
                    case DataTypeEnums.Int32:
                    case DataTypeEnums.Float:
                        dataLength = Length * 4;
                        break;
                    case DataTypeEnums.Double:
                        dataLength = Length * 8;
                        break;
                    case DataTypeEnums.String:
                        dataLength = Length;
                        break;
                    default:
                        dataLength = Length * 2; // 默认按字处理
                        break;
                }
            }

            return headerLength + dataLength;
        }

        /// <summary>
        /// 创建简单读取请求
        /// </summary>
        /// <param name="address">地址字符串</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="length">读取长度</param>
        /// <returns>读取请求</returns>
        public static FinsReadRequest Create(string address, DataTypeEnums dataType = DataTypeEnums.UInt16, int length = 1)
        {
            return new FinsReadRequest(address, length, dataType);
        }

        /// <summary>
        /// 初始化消息
        /// </summary>
        /// <param name="data">初始化数据</param>
        public void Initialize(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                ProtocolMessageFrame = data;
            }
        }

        /// <summary>
        /// 获取消息描述
        /// </summary>
        /// <returns>消息描述</returns>
        public override string ToString()
        {
            return $"FINS读取请求 - 地址: {Address}, 长度: {Length}, 数据类型: {DataType}";
        }
    }
}