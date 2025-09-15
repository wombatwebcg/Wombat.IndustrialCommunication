using System;
using System.Collections.Generic;
using System.Linq;
using Wombat.Extensions.DataTypeExtensions;


namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS读取响应消息类
    /// </summary>
    public class FinsReadResponse : IDeviceReadWriteMessage
    {
        /// <summary>
        /// 协议消息帧
        /// </summary>
        public byte[] ProtocolMessageFrame { get; private set; }

        /// <summary>
        /// 响应头信息
        /// </summary>
        public FinsResponseHeader Header { get; private set; }

        /// <summary>
        /// 响应数据
        /// </summary>
        public byte[] Data { get; private set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public DataTypeEnums DataType { get; private set; }

        /// <summary>
        /// 是否为位操作
        /// </summary>
        public bool IsBitOperation { get; private set; }

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
        /// <param name="responseData">响应数据</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isBitOperation">是否为位操作</param>
        public FinsReadResponse(byte[] responseData, DataTypeEnums dataType = DataTypeEnums.UInt16, bool isBitOperation = false)
        {
            ProtocolMessageFrame = responseData ?? throw new ArgumentNullException(nameof(responseData));
            DataType = dataType;
            IsBitOperation = isBitOperation;
            ParseResponse();
        }

        /// <summary>
        /// 解析响应数据
        /// </summary>
        private void ParseResponse()
        {
            try
            {
                if (ProtocolMessageFrame.Length < 12)
                {
                    IsSuccess = false;
                    ErrorMessage = "响应数据长度不足";
                    Data = new byte[0]; // 长度不足时初始化为空数组
                    return;
                }

                // 解析响应头
                Header = FinsCommonMethods.ParseFinsResponseHeader(ProtocolMessageFrame);

                // 检查响应是否成功
                IsSuccess = FinsCommonMethods.IsResponseSuccess(Header);

                if (!IsSuccess)
                {
                    ErrorMessage = FinsCommonMethods.GetErrorDescription(Header.MRC, Header.SRC);
                    Data = new byte[0]; // 错误情况下初始化为空数组
                    return;
                }

                // 提取数据部分
                if (ProtocolMessageFrame.Length > 12)
                {
                    int dataLength = ProtocolMessageFrame.Length - 12;
                    Data = new byte[dataLength];
                    Array.Copy(ProtocolMessageFrame, 12, Data, 0, dataLength);
                }
                else
                {
                    Data = new byte[0];
                }
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                ErrorMessage = $"解析响应数据时发生错误: {ex.Message}";
                Data = new byte[0];
            }
        }

        /// <summary>
        /// 获取布尔值
        /// </summary>
        /// <returns>布尔值</returns>
        public bool GetBoolean()
        {
            if (!IsSuccess || Data == null || Data.Length == 0)
                return false;

            if (IsBitOperation)
            {
                return Data[0] != 0;
            }
            else
            {
                return BitConverter.ToUInt16(Data, 0) != 0;
            }
        }

        /// <summary>
        /// 获取字节值
        /// </summary>
        /// <returns>字节值</returns>
        public byte GetByte()
        {
            if (!IsSuccess || Data == null || Data.Length == 0)
                return 0;

            return Data[0];
        }

        /// <summary>
        /// 获取16位有符号整数
        /// </summary>
        /// <returns>16位有符号整数</returns>
        public short GetInt16()
        {
            if (!IsSuccess || Data == null || Data.Length < 2)
                return 0;

            return BitConverter.ToInt16(Data, 0);
        }

        /// <summary>
        /// 获取16位无符号整数
        /// </summary>
        /// <returns>16位无符号整数</returns>
        public ushort GetUInt16()
        {
            if (!IsSuccess || Data == null || Data.Length < 2)
                return 0;

            return BitConverter.ToUInt16(Data, 0);
        }

        /// <summary>
        /// 获取32位有符号整数
        /// </summary>
        /// <returns>32位有符号整数</returns>
        public int GetInt32()
        {
            if (!IsSuccess || Data == null || Data.Length < 4)
                return 0;

            return BitConverter.ToInt32(Data, 0);
        }

        /// <summary>
        /// 获取32位无符号整数
        /// </summary>
        /// <returns>32位无符号整数</returns>
        public uint GetUInt32()
        {
            if (!IsSuccess || Data == null || Data.Length < 4)
                return 0;

            return BitConverter.ToUInt32(Data, 0);
        }

        /// <summary>
        /// 获取单精度浮点数
        /// </summary>
        /// <returns>单精度浮点数</returns>
        public float GetFloat()
        {
            if (!IsSuccess || Data == null || Data.Length < 4)
                return 0.0f;

            return BitConverter.ToSingle(Data, 0);
        }

        /// <summary>
        /// 获取双精度浮点数
        /// </summary>
        /// <returns>双精度浮点数</returns>
        public double GetDouble()
        {
            if (!IsSuccess || Data == null || Data.Length < 8)
                return 0.0;

            return BitConverter.ToDouble(Data, 0);
        }

        /// <summary>
        /// 获取字符串
        /// </summary>
        /// <param name="encoding">编码方式</param>
        /// <returns>字符串</returns>
        public string GetString(System.Text.Encoding encoding = null)
        {
            if (!IsSuccess || Data == null || Data.Length == 0)
                return string.Empty;

            encoding = encoding ?? System.Text.Encoding.ASCII;
            return encoding.GetString(Data).TrimEnd('\0');
        }

        /// <summary>
        /// 获取字节数组
        /// </summary>
        /// <returns>字节数组</returns>
        public byte[] GetBytes()
        {
            if (!IsSuccess || Data == null)
                return new byte[0];

            var result = new byte[Data.Length];
            Array.Copy(Data, result, Data.Length);
            return result;
        }

        /// <summary>
        /// 根据数据类型获取值
        /// </summary>
        /// <returns>转换后的值</returns>
        public object GetValue()
        {
            if (!IsSuccess)
                return null;

            switch (DataType)
            {
                case DataTypeEnums.Bool:
                    return GetBoolean();
                case DataTypeEnums.Byte:
                    return GetByte();
                case DataTypeEnums.Int16:
                    return GetInt16();
                case DataTypeEnums.UInt16:
                    return GetUInt16();
                case DataTypeEnums.Int32:
                    return GetInt32();
                case DataTypeEnums.UInt32:
                    return GetUInt32();
                case DataTypeEnums.Float:
                    return GetFloat();
                case DataTypeEnums.Double:
                    return GetDouble();
                case DataTypeEnums.String:
                    return GetString();
                default:
                    return GetBytes();
            }
        }

        /// <summary>
        /// 获取多个值（用于批量读取）
        /// </summary>
        /// <param name="addresses">地址列表</param>
        /// <returns>值列表</returns>
        public List<object> GetValues(List<FinsAddress> addresses)
        {
            var results = new List<object>();

            if (!IsSuccess || Data == null || addresses == null)
                return results;

            int offset = 0;
            foreach (var address in addresses)
            {
                try
                {
                    int dataLength = FinsCommonMethods.GetDataTypeLength(address.DataType);
                    
                    if (address.IsBit)
                    {
                        dataLength = 1;
                    }
                    else if (address.DataType == DataTypeEnums.UInt16 || address.DataType == DataTypeEnums.Int16)
                    {
                        dataLength = 2; // FINS中字数据为2字节
                    }
                    else if (address.DataType == DataTypeEnums.UInt32 || address.DataType == DataTypeEnums.Int32 || address.DataType == DataTypeEnums.Float)
                    {
                        dataLength = 4; // FINS中双字数据为4字节
                    }

                    if (offset + dataLength <= Data.Length)
                    {
                        byte[] valueData = new byte[dataLength];
                        Array.Copy(Data, offset, valueData, 0, dataLength);
                        
                        object value = FinsCommonMethods.ConvertFromBytes(valueData, address.DataType);
                        results.Add(value);
                        
                        offset += dataLength;
                    }
                    else
                    {
                        results.Add(null);
                    }
                }
                catch
                {
                    results.Add(null);
                }
            }

            return results;
        }

        /// <summary>
        /// 获取16位整数数组
        /// </summary>
        /// <returns>16位整数数组</returns>
        public ushort[] GetUInt16Array()
        {
            if (!IsSuccess || Data == null || Data.Length == 0)
                return new ushort[0];

            int count = Data.Length / 2;
            var result = new ushort[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = BitConverter.ToUInt16(Data, i * 2);
            }

            return result;
        }

        /// <summary>
        /// 获取32位整数数组
        /// </summary>
        /// <returns>32位整数数组</returns>
        public uint[] GetUInt32Array()
        {
            if (!IsSuccess || Data == null || Data.Length == 0)
                return new uint[0];

            int count = Data.Length / 4;
            var result = new uint[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = BitConverter.ToUInt32(Data, i * 4);
            }

            return result;
        }

        /// <summary>
        /// 获取浮点数数组
        /// </summary>
        /// <returns>浮点数数组</returns>
        public float[] GetFloatArray()
        {
            if (!IsSuccess || Data == null || Data.Length == 0)
                return new float[0];

            int count = Data.Length / 4;
            var result = new float[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = BitConverter.ToSingle(Data, i * 4);
            }

            return result;
        }

        /// <summary>
        /// 创建成功响应
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isBitOperation">是否为位操作</param>
        /// <returns>响应实例</returns>
        public static FinsReadResponse CreateSuccess(byte[] data, DataTypeEnums dataType = DataTypeEnums.UInt16, bool isBitOperation = false)
        {
            // 构造成功的响应帧
            var responseFrame = new List<byte>();
            
            // 添加成功的响应头
            responseFrame.AddRange(FinsCommonMethods.BuildFinsHeader(
                icf: 0xC0,  // 响应标志
                rsv: 0x00,
                gct: 0x02,
                dna: 0x00,
                da1: 0x00,
                da2: 0x00,
                sna: 0x00,
                sa1: 0x00,
                sa2: 0x00,
                sid: 0x00
            ));
            
            // 添加成功的响应码
            responseFrame.Add(0x00); // MRC: 正常完成
            responseFrame.Add(0x00); // SRC: 正常完成
            
            // 添加数据
            if (data != null && data.Length > 0)
            {
                responseFrame.AddRange(data);
            }

            return new FinsReadResponse(responseFrame.ToArray(), dataType, isBitOperation);
        }

        /// <summary>
        /// 创建错误响应
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <returns>响应实例</returns>
        public static FinsReadResponse CreateError(ushort errorCode)
        {
            // 构造错误的响应帧
            var responseFrame = new List<byte>();
            
            // 添加错误的响应头
            responseFrame.AddRange(FinsCommonMethods.BuildFinsHeader(
                icf: 0xC0,  // 响应标志
                rsv: 0x00,
                gct: 0x02,
                dna: 0x00,
                da1: 0x00,
                da2: 0x00,
                sna: 0x00,
                sa1: 0x00,
                sa2: 0x00,
                sid: 0x00
            ));
            
            // 添加错误的响应码
            responseFrame.Add((byte)(errorCode >> 8));   // MRC
            responseFrame.Add((byte)(errorCode & 0xFF)); // SRC

            return new FinsReadResponse(responseFrame.ToArray());
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
                ParseResponse();
            }
        }

        /// <summary>
        /// 获取响应描述
        /// </summary>
        /// <returns>响应描述</returns>
        public override string ToString()
        {
            if (IsSuccess)
            {
                return $"FINS读取响应 - 成功, 数据长度: {Data?.Length ?? 0}字节";
            }
            else
            {
                return $"FINS读取响应 - 失败: {ErrorMessage}";
            }
        }
    }
}