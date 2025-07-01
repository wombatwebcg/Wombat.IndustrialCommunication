using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// S7��������ö��
    /// </summary>
    public enum S7DataType
    {
        // DB�������ݿ飩
        DBX,  // DBλ
        DBB,  // DB�ֽ�
        DBW,  // DB��
        DBD,  // DB˫��
        
        // I������������- λ��ֱַ���ڽ���ʱ��������Ҫ������IX����
        IB,   // I���ֽ�
        IW,   // I����
        ID,   // I��˫��
        
        // Q�����������- λ��ֱַ���ڽ���ʱ��������Ҫ������QX����  
        QB,   // Q���ֽ�
        QW,   // Q����
        QD,   // Q��˫��
        
        // M����Merker�ڲ��洢����
        MX,   // M��λ
        MB,   // M���ֽ�
        MW,   // M����
        MD,   // M��˫��
        
        // V����Smart200ר�ã�ӳ�䵽DB1��
        VB,   // V���ֽڣ�Smart200�ã�
        VW,   // V���֣�Smart200�ã�
        VD    // V��˫�֣�Smart200�ã�
    }

    /// <summary>
    /// S7��ַ��Ϣ�ṹ��
    /// </summary>
    public struct S7AddressInfo
    {
        public string OriginalAddress { get; set; }
        public int DbNumber { get; set; }
        public int StartByte { get; set; }
        public int Length { get; set; }
        public S7DataType DataType { get; set; }
        public int BitOffset { get; set; }  // λƫ�ƣ�����DBX������Ч
    }

    /// <summary>
    /// S7�Ż���ַ��
    /// </summary>
    public class S7AddressBlock
    {
        public int DbNumber { get; set; }
        public int StartByte { get; set; }
        public int TotalLength { get; set; }
        public List<S7AddressInfo> Addresses { get; set; } = new List<S7AddressInfo>();
        public double EfficiencyRatio { get; set; }
    }

    public class S7Communication : DeviceDataReaderWriterBase
    {

        internal AsyncLock _lock = new AsyncLock();


        private static ArrayPool<byte> _byteArrayPool = ArrayPool<byte>.Shared;

        public S7Communication(S7EthernetTransport s7EthernetTransport) :base(s7EthernetTransport)
        {
            DataFormat = Extensions.DataTypeExtensions.EndianFormat.ABCD;
            IsReverse = true;
        }


        public override string Version => SiemensVersion.ToString();

        /// <summary>
        /// ��ۺ� 
        /// </summary>
        public byte Slot { get; set; }

        /// <summary>
        /// ���ܺ�
        /// </summary>
        public byte Rack { get;set; }


        public SiemensVersion SiemensVersion{ get; set; }

        public async Task<OperationResult> InitAsync()
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                try
                {
                    var command1 = SiemensConstant.Command1;
                    var command2 = SiemensConstant.Command2;

                    switch (SiemensVersion)
                    {
                        case SiemensVersion.S7_200:
                            command1 = SiemensConstant.Command1_200;
                            command2 = SiemensConstant.Command2_200;
                            break;
                        case SiemensVersion.S7_200Smart:
                            command1 = SiemensConstant.Command1_200Smart;
                            command2 = SiemensConstant.Command2_200Smart;
                            break;
                        case SiemensVersion.S7_300:
                            command1[21] = (byte)((Rack * 0x20) + Slot); //0x02;
                            break;
                        case SiemensVersion.S7_400:
                            command1[21] = (byte)((Rack * 0x20) + Slot); //0x03;
                            command1[17] = 0x00;
                            break;
                        case SiemensVersion.S7_1200:
                            command1[21] = (byte)((Rack * 0x20) + Slot); //0x00;
                            break;
                        case SiemensVersion.S7_1500:
                            command1[21] = (byte)((Rack * 0x20) + Slot); //0x00;
                            break;
                        default:
                            command1[18] = 0x00;
                            break;
                    }

                    result.Requsts.Add(string.Join(" ", command1.Select(t => t.ToString("X2"))));
                    var command1RequestResult = await Transport.SendRequestAsync(command1);
                    if (command1RequestResult.IsSuccess)
                    {
                        var response1Result = await Transport.ReceiveResponseAsync(0, SiemensConstant.InitHeadLength);
                        if (response1Result.IsSuccess)
                        {
                            var response1 = response1Result.ResultValue;
                            var response2Result = await Transport.ReceiveResponseAsync(0, S7CommonMethods.GetContentLength(response1));
                            if (!response2Result.IsSuccess)
                            {
                                return response2Result;

                            }
                            var response2 = response1Result.ResultValue;
                            result.Responses.Add(string.Join(" ", response1.Concat(response2).Select(t => t.ToString("X2"))));

                        }

                    }

                    result.Requsts.Add(string.Join(" ", command2.Select(t => t.ToString("X2"))));
                    //�ڶ��γ�ʼ��ָ���
                    var command2RequestResult = await Transport.SendRequestAsync(command2);
                    if (command2RequestResult.IsSuccess)
                    {
                        var response3Result = await Transport.ReceiveResponseAsync(0, SiemensConstant.InitHeadLength);
                        if (!response3Result.IsSuccess)
                        {
                            return response3Result;

                        }
                        var response3 = response3Result.ResultValue;

                        var response4Result = await Transport.ReceiveResponseAsync(0, S7CommonMethods.GetContentLength(response3));
                        if (!response4Result.IsSuccess)
                        {
                            return response4Result;

                        }
                        var response4 = response4Result.ResultValue;
                        result.Responses.Add(string.Join(" ", response3.Concat(response4).Select(t => t.ToString("X2"))));
                    }


                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                    result.ErrorCode = 408;
                    result.Exception = ex;
                }
                return result.Complete();
            }
        }


        internal override  async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (Transport is S7EthernetTransport s7Transport)
                {
                    int maxCount = 180;
                    if (length > maxCount)
                    {
                        int alreadyFinished = 0;
                        List<byte> bytesContent = new List<byte>();
                        while (alreadyFinished < length)
                        {
                            ushort readLength = (ushort)Math.Min(length - alreadyFinished, maxCount);

                            var tempResult = await internalReadAsync(s7Transport, address,alreadyFinished, readLength, isBit);
                            if (tempResult.IsSuccess)
                            {
                                result.Requsts.Add(tempResult.Requsts[0]);
                                result.Responses.Add(tempResult.Responses[0]);
                                bytesContent.AddRange(tempResult.ResultValue);
                                alreadyFinished += readLength;
                            }
                            else
                            {
                                // ��ȡʧ�ܣ�ֱ�ӷ���ʧ�ܽ������������ѭ��
                                return tempResult;
                            }
                        }

                        result.ResultValue = bytesContent.ToArray();
                        return result.Complete();


                    }
                    else
                    {
                        return await internalReadAsync(s7Transport, address,0, length, isBit);
                    }

                }
                return OperationResult.CreateFailedResult<byte[]>();
            }

            async ValueTask<OperationResult<byte[]>> internalReadAsync(S7EthernetTransport transport,string internalAddress,int internalOffest, int internalLength, bool internalIsBit = false)
            {
                var tempResult = new OperationResult<byte>();
                var readRequest = new S7ReadRequest(internalAddress, internalOffest, internalLength, isBit);
                var response = await transport.UnicastReadMessageAsync(readRequest);
                if (response.IsSuccess)
                {
                    int realLength = internalLength;
                    var dataPackage = response.ResultValue.ProtocolMessageFrame;
                    byte[] responseData = new byte[realLength];
                    try
                    {
                        //0x04 �� 0x01 ��ȡһ������ //�����������ȡ��������ȡ������������֤
                        if (dataPackage[19] == 0x04 && dataPackage[20] == 0x01)
                        {
                            if (dataPackage[21] == 0x0A && dataPackage[22] == 0x00)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"��ȡ{internalAddress}ʧ�ܣ���ȷ���Ƿ���ڵ�ַ{internalAddress}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                            else if (dataPackage[21] == 0x05 && dataPackage[22] == 0x00)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"��ȡ{internalAddress}ʧ�ܣ���ȷ���Ƿ���ڵ�ַ{internalAddress}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                            else if (dataPackage[21] != 0xFF)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"��ȡ{internalAddress}ʧ�ܣ��쳣����[{21}]:{dataPackage[21]}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                        }
                        if (internalIsBit) { realLength = (int)(Math.Ceiling(realLength / 8.0)); }
                        Array.Copy(dataPackage, dataPackage.Length - realLength, responseData, 0, realLength);
                    }
                    catch (Exception ex)
                    {
                        tempResult.Exception = ex;
                        tempResult.Message = $"{internalAddress} {internalOffest} {internalLength} ��ȡԤ�ڳ����뷵�����ݳ��Ȳ�һ��";
                        return OperationResult.CreateFailedResult<byte[]>(tempResult);
                    }
                    return new OperationResult<byte[]>(response, responseData).Complete();
                }
                else
                {

                    return OperationResult.CreateFailedResult<byte[]>(response);
                }
            }

        }

        internal override async Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte> result = new OperationResult<byte>();
                if (Transport is S7EthernetTransport s7Transport)
                {
                    var writeRequest = new S7WriteRequest(address,0,data, isBit);
                    var response = await s7Transport.UnicastWriteMessageAsync(writeRequest);
                    if (response.IsSuccess)
                    {
                        var dataPackage = response.ResultValue.ProtocolMessageFrame;
                        var offset = dataPackage.Length - 1;
                        if (dataPackage[offset] == 0x0A)
                        {
                            result.IsSuccess = false;
                            result.Message = $"д��{address}ʧ�ܣ���ȷ���Ƿ���ڵ�ַ{address}���쳣����[{offset}]:{dataPackage[offset]}";
                        }
                        else if (dataPackage[offset] == 0x05)
                        {
                            result.IsSuccess = false;
                            result.Message = $"д��{address}ʧ�ܣ���ȷ���Ƿ���ڵ�ַ{address}���쳣����[{offset}]:{dataPackage[offset]}";
                        }
                        else if (dataPackage[offset] != 0xFF)
                        {
                            result.IsSuccess = false;
                            result.Message = $"д��{address}ʧ�ܣ��쳣����[{offset}]:{dataPackage[offset]}";
                        }
                        return OperationResult.CreateSuccessResult(response);
                    }
                    else
                    {

                        return OperationResult.CreateFailedResult(response);
                    }
                }
                return OperationResult.CreateFailedResult();
            }
        }

        /// <summary>
        /// ������ַ�ַ���Ϊ�ṹ����Ϣ
        /// </summary>
        /// <param name="addresses">��ַ�ֵ�</param>
        /// <returns>������ĵ�ַ��Ϣ�б�</returns>
        private List<S7AddressInfo> ParseAddresses(Dictionary<string, object> addresses)
        {
            var addressInfos = new List<S7AddressInfo>();
            
            foreach (var kvp in addresses)
            {
                try
                {
                    var addressInfo = ParseSingleAddress(kvp.Key);
                    
                    // ����V����Q����ַ�����Ը����������ƶ��������ͺͳ���
                    if (addressInfo.DbNumber == -1 || addressInfo.DbNumber == -2)
                    {
                        addressInfo = InferDataTypeAndLength(addressInfo, kvp.Key);
                    }
                    
                    addressInfos.Add(addressInfo);
                }
                catch (Exception ex)
                {
                    // ��ַ����ʧ�ܣ������õ�ַ������¼��־
                    // ������������־��¼
                    continue;
                }
            }
            
            return addressInfos;
        }

        /// <summary>
        /// �ƶ�V����Q����ַ���������ͺͳ���
        /// </summary>
        /// <param name="addressInfo">ԭʼ��ַ��Ϣ</param>
        /// <param name="originalAddress">ԭʼ��ַ�ַ���</param>
        /// <returns>�ƶϺ�ĵ�ַ��Ϣ</returns>
        private S7AddressInfo InferDataTypeAndLength(S7AddressInfo addressInfo, string originalAddress)
        {
            // ����V����ַ��Ĭ���ƶ�Ϊ32λ�������ͣ��ܼ��ݴ���������
            if (addressInfo.DbNumber == -1) // V��
            {
                addressInfo.DataType = S7DataType.DBD; // ʹ��DB����˫������
                addressInfo.Length = 4;
            }
            // ����Q����ַ�����û��λ��ʶ��Ĭ���ƶ�Ϊ������
            else if (addressInfo.DbNumber == -2 && !originalAddress.Contains(".")) // Q���ҷ�λ��ַ
            {
                addressInfo.DataType = S7DataType.DBW; // ʹ��DB����������
                addressInfo.Length = 2;
            }
            // ����I����ַ�����û��λ��ʶ��Ĭ���ƶ�Ϊ������
            else if (addressInfo.DbNumber == -3 && !originalAddress.Contains(".")) // I���ҷ�λ��ַ
            {
                addressInfo.DataType = S7DataType.IW; // ʹ��I����������
                addressInfo.Length = 2;
            }

            return addressInfo;
        }

        /// <summary>
        /// ����������ַ�ַ���
        /// </summary>
        /// <param name="address">��ַ�ַ������� "DB1.DBW10", "DB2.DBX5.3", "V700", "Q1.3"</param>
        /// <returns>��ַ��Ϣ</returns>
        private S7AddressInfo ParseSingleAddress(string address)
        {
            var addressInfo = new S7AddressInfo
            {
                OriginalAddress = address
            };

            // ͳһת��Ϊ��д��ȥ���ո�
            address = address.ToUpper().Replace(" ", "");

            // ����Ƿ���DB��ַ��ʽ
            if (address.StartsWith("DB"))
            {
                return ParseDBAddress(address, addressInfo);
            }
            // ����Ƿ���V����ַ��ʽ
            else if (address.StartsWith("V"))
            {
                return ParseVAddress(address, addressInfo);
            }
            // ����Ƿ���Q����ַ��ʽ
            else if (address.StartsWith("Q"))
            {
                return ParseQAddress(address, addressInfo);
            }
            // ����Ƿ���I����ַ��ʽ
            else if (address.StartsWith("I"))
            {
                return ParseIAddress(address, addressInfo);
            }
            // ����Ƿ���M����ַ��ʽ
            else if (address.StartsWith("M"))
            {
                return ParseMAddress(address, addressInfo);
            }
            else
            {
                throw new ArgumentException($"��֧�ֵĵ�ַ��ʽ: {address}");
            }
        }

        /// <summary>
        /// ����DB��ַ��ʽ
        /// </summary>
        private S7AddressInfo ParseDBAddress(string address, S7AddressInfo addressInfo)
        {
            var dbEndIndex = address.IndexOf('.');
            if (dbEndIndex == -1)
                throw new ArgumentException($"DB��ַ��ʽ����: {address}");

            var dbNumberStr = address.Substring(2, dbEndIndex - 2);
            if (!int.TryParse(dbNumberStr, out int dbNumber))
                throw new ArgumentException($"DB�Ž���ʧ��: {address}");

            addressInfo.DbNumber = dbNumber;

            // �����������ͺ�ƫ��
            var typeAndOffset = address.Substring(dbEndIndex + 1);
            
            if (typeAndOffset.StartsWith("DBX"))
            {
                // λ��ַ���� DBX5.3
                addressInfo.DataType = S7DataType.DBX;
                addressInfo.Length = 1; // λ����Ϊ1

                var parts = typeAndOffset.Substring(3).Split('.');
                if (parts.Length != 2)
                    throw new ArgumentException($"λ��ַ��ʽ����: {address}");

                if (!int.TryParse(parts[0], out int byteOffset) || !int.TryParse(parts[1], out int bitOffset))
                    throw new ArgumentException($"λ��ַƫ�ƽ���ʧ��: {address}");

                addressInfo.StartByte = byteOffset;
                addressInfo.BitOffset = bitOffset;
            }
            else if (typeAndOffset.StartsWith("DBB"))
            {
                // �ֽڵ�ַ
                addressInfo.DataType = S7DataType.DBB;
                addressInfo.Length = 1;
                addressInfo.BitOffset = 0;

                var offsetStr = typeAndOffset.Substring(3);
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"�ֽڵ�ַƫ�ƽ���ʧ��: {address}");

                addressInfo.StartByte = offset;
            }
            else if (typeAndOffset.StartsWith("DBW"))
            {
                // �ֵ�ַ
                addressInfo.DataType = S7DataType.DBW;
                addressInfo.Length = 2;
                addressInfo.BitOffset = 0;

                var offsetStr = typeAndOffset.Substring(3);
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"�ֵ�ַƫ�ƽ���ʧ��: {address}");

                addressInfo.StartByte = offset;
            }
            else if (typeAndOffset.StartsWith("DBD"))
            {
                // ˫�ֵ�ַ
                addressInfo.DataType = S7DataType.DBD;
                addressInfo.Length = 4;
                addressInfo.BitOffset = 0;

                var offsetStr = typeAndOffset.Substring(3);
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"˫�ֵ�ַƫ�ƽ���ʧ��: {address}");

                addressInfo.StartByte = offset;
            }
            else
            {
                throw new ArgumentException($"��֧�ֵ�DB��������: {address}");
            }

            return addressInfo;
        }

        /// <summary>
        /// ����V����ַ��ʽ
        /// </summary>
        private S7AddressInfo ParseVAddress(string address, S7AddressInfo addressInfo)
        {
            var offsetStr = address.Substring(1); // ȥ��Vǰ׺
            if (!int.TryParse(offsetStr, out int offset))
                throw new ArgumentException($"V����ַƫ�ƽ���ʧ��: {address}");

            // ����Smart200��V����ַӳ�䵽DB1
            if (SiemensVersion == SiemensVersion.S7_200Smart)
            {
                addressInfo.DbNumber = 1; // Smart200��V����ӦDB1
                addressInfo.DataType = S7DataType.DBB; // ʹ��DB����������
                addressInfo.StartByte = offset;
                addressInfo.Length = 1; // Ĭ�ϳ��ȣ�ʵ��ʹ��ʱ�������Ҫ����
                addressInfo.BitOffset = 0;
            }
            else
            {
                // �����汾��V����ַʹ�������DB�ű�ʶ
                addressInfo.DbNumber = -1; // ʹ��-1��ʾV��
                addressInfo.DataType = S7DataType.DBB; // ͳһʹ��DB����������
                addressInfo.StartByte = offset;
                addressInfo.Length = 1; // Ĭ�ϳ��ȣ�ʵ��ʹ��ʱ�������Ҫ����
                addressInfo.BitOffset = 0;
            }

            return addressInfo;
        }

        /// <summary>
        /// ����Q����ַ��ʽ
        /// </summary>
        private S7AddressInfo ParseQAddress(string address, S7AddressInfo addressInfo)
        {
            // Q����ַ�����������ʹ�������DB�ű�ʶ
            addressInfo.DbNumber = -2; // ʹ��-2��ʾQ��

            if (address.Contains("."))
            {
                // Q��λ��ַ���� Q1.3
                var parts = address.Substring(1).Split('.'); // ȥ��Qǰ׺���ָ�
                if (parts.Length != 2)
                    throw new ArgumentException($"Q��λ��ַ��ʽ����: {address}");

                if (!int.TryParse(parts[0], out int byteOffset) || !int.TryParse(parts[1], out int bitOffset))
                    throw new ArgumentException($"Q��λ��ַƫ�ƽ���ʧ��: {address}");

                addressInfo.DataType = S7DataType.DBX; // Q��λ��ַʹ��DBλ����
                addressInfo.StartByte = byteOffset;
                addressInfo.BitOffset = bitOffset;
                addressInfo.Length = 1; // λ����Ϊ1
            }
            else
            {
                // Q���ֽڵ�ַ���� Q10
                var offsetStr = address.Substring(1); // ȥ��Qǰ׺
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"Q����ַƫ�ƽ���ʧ��: {address}");

                addressInfo.DataType = S7DataType.DBW; // Q���ֵ�ַʹ��DB������
                addressInfo.StartByte = offset;
                addressInfo.Length = 2;
                addressInfo.BitOffset = 0;
            }

            return addressInfo;
        }

        /// <summary>
        /// ����I����ַ��ʽ
        /// </summary>
        private S7AddressInfo ParseIAddress(string address, S7AddressInfo addressInfo)
        {
            // I����ַ������������ʹ�������DB�ű�ʶ
            addressInfo.DbNumber = -3; // ʹ��-3��ʾI��

            if (address.Contains("."))
            {
                // I��λ��ַ���� I1.3
                var parts = address.Substring(1).Split('.'); // ȥ��Iǰ׺���ָ�
                if (parts.Length != 2)
                    throw new ArgumentException($"I��λ��ַ��ʽ����: {address}");

                if (!int.TryParse(parts[0], out int byteOffset) || !int.TryParse(parts[1], out int bitOffset))
                    throw new ArgumentException($"I��λ��ַƫ�ƽ���ʧ��: {address}");

                addressInfo.DataType = S7DataType.DBX; // I��λ��ַʹ��DBX���ͱ�ʾ
                addressInfo.StartByte = byteOffset;
                addressInfo.BitOffset = bitOffset;
                addressInfo.Length = 1; // λ����Ϊ1
            }
            else
            {
                // I���ֽڵ�ַ���� I10
                var offsetStr = address.Substring(1); // ȥ��Iǰ׺
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"I����ַƫ�ƽ���ʧ��: {address}");

                addressInfo.DataType = S7DataType.IW; // Ĭ�ϰ��ִ���
                addressInfo.StartByte = offset;
                addressInfo.Length = 2;
                addressInfo.BitOffset = 0;
            }

            return addressInfo;
        }

        /// <summary>
        /// ����M����ַ��ʽ
        /// </summary>
        private S7AddressInfo ParseMAddress(string address, S7AddressInfo addressInfo)
        {
            // M����ַ�����ڲ��洢����ʹ�������DB�ű�ʶ
            addressInfo.DbNumber = -4; // ʹ��-4��ʾM��

            if (address.Contains("."))
            {
                // M��λ��ַ���� M1.3
                var parts = address.Substring(1).Split('.'); // ȥ��Mǰ׺���ָ�
                if (parts.Length != 2)
                    throw new ArgumentException($"M��λ��ַ��ʽ����: {address}");

                if (!int.TryParse(parts[0], out int byteOffset) || !int.TryParse(parts[1], out int bitOffset))
                    throw new ArgumentException($"M��λ��ַƫ�ƽ���ʧ��: {address}");

                addressInfo.DataType = S7DataType.MX;
                addressInfo.StartByte = byteOffset;
                addressInfo.BitOffset = bitOffset;
                addressInfo.Length = 1; // λ����Ϊ1
            }
            else
            {
                // M���ֽڵ�ַ���� M10
                var offsetStr = address.Substring(1); // ȥ��Mǰ׺
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"M����ַƫ�ƽ���ʧ��: {address}");

                addressInfo.DataType = S7DataType.MB; // Ĭ�ϰ��ֽڴ���
                addressInfo.StartByte = offset;
                addressInfo.Length = 1;
                addressInfo.BitOffset = 0;
            }

            return addressInfo;
        }

        /// <summary>
        /// ��ȡ��ַ����������
        /// </summary>
        /// <param name="dataType">��������</param>
        /// <returns>���������ַ���</returns>
        private string GetAreaType(S7DataType dataType)
        {
            switch (dataType)
            {
                case S7DataType.DBX:
                case S7DataType.DBB:
                case S7DataType.DBW:
                case S7DataType.DBD:
                    return "DB";
                case S7DataType.IB:
                case S7DataType.IW:
                case S7DataType.ID:
                    return "I";
                case S7DataType.QB:
                case S7DataType.QW:
                case S7DataType.QD:
                    return "Q";
                case S7DataType.MX:
                case S7DataType.MB:
                case S7DataType.MW:
                case S7DataType.MD:
                    return "M";
                case S7DataType.VB:
                case S7DataType.VW:
                case S7DataType.VD:
                    return "V";
                default:
                    return "UNKNOWN";
            }
        }

        /// <summary>
        /// ��̬�����Ż��㷨������ַ�ϲ�Ϊ��Ч�Ķ�ȡ��
        /// </summary>
        /// <param name="addressInfos">��ַ��Ϣ�б�</param>
        /// <param name="minEfficiencyRatio">��СЧ�ʱȣ���Ч����/�ܶ�ȡ���ݣ�</param>
        /// <param name="maxBlockSize">�����С���ֽڣ�</param>
        /// <returns>�Ż���ĵ�ַ���б�</returns>
        private List<S7AddressBlock> OptimizeAddressBlocks(List<S7AddressInfo> addressInfos, double minEfficiencyRatio = 0.7, int maxBlockSize = 180)
        {
            var optimizedBlocks = new List<S7AddressBlock>();
            
            // ���������ͺ�DB�ŷ��飨DB��ַ��DB�ŷ��飬V����Q���ֱ�������飩
            var dbGroups = addressInfos.GroupBy(a => new { a.DbNumber, AreaType = GetAreaType(a.DataType) }).ToList();
            
            foreach (var dbGroup in dbGroups)
            {
                // ��ÿ�������ڰ���ʼ��ַ����
                var sortedAddresses = dbGroup.OrderBy(a => a.StartByte).ToList();
                
                var currentBlock = new S7AddressBlock
                {
                    DbNumber = dbGroup.Key.DbNumber,
                    Addresses = new List<S7AddressInfo>()
                };
                
                foreach (var address in sortedAddresses)
                {
                    // ����ǵ�һ����ַ��ֱ�Ӽ��뵱ǰ��
                    if (currentBlock.Addresses.Count == 0)
                    {
                        currentBlock.StartByte = address.StartByte;
                        currentBlock.TotalLength = address.Length;
                        currentBlock.Addresses.Add(address);
                        continue;
                    }
                    
                    // �����������˵�ַ����¿����
                    var newStartByte = Math.Min(currentBlock.StartByte, address.StartByte);
                    var currentEndByte = currentBlock.StartByte + currentBlock.TotalLength;
                    var addressEndByte = address.StartByte + address.Length;
                    var newEndByte = Math.Max(currentEndByte, addressEndByte);
                    var newTotalLength = newEndByte - newStartByte;
                    
                    // �����С����
                    if (newTotalLength > maxBlockSize)
                    {
                        // ���������С����ɵ�ǰ�鲢��ʼ�¿�
                        currentBlock.EfficiencyRatio = CalculateEfficiencyRatio(currentBlock);
                        optimizedBlocks.Add(currentBlock);
                        
                        currentBlock = new S7AddressBlock
                        {
                            DbNumber = dbGroup.Key.DbNumber,
                            StartByte = address.StartByte,
                            TotalLength = address.Length,
                            Addresses = new List<S7AddressInfo> { address }
                        };
                        continue;
                    }
                    
                    // ���������Ч�ʱ�
                    var testBlock = new S7AddressBlock
                    {
                        DbNumber = dbGroup.Key.DbNumber,
                        StartByte = newStartByte,
                        TotalLength = newTotalLength,
                        Addresses = new List<S7AddressInfo>(currentBlock.Addresses) { address }
                    };
                    
                    var newEfficiencyRatio = CalculateEfficiencyRatio(testBlock);
                    
                    // ���Ч�ʱ��Ƿ�����Ҫ��
                    if (newEfficiencyRatio >= minEfficiencyRatio)
                    {
                        // Ч�ʱ�����Ҫ�󣬺ϲ���ַ
                        currentBlock.StartByte = newStartByte;
                        currentBlock.TotalLength = newTotalLength;
                        currentBlock.Addresses.Add(address);
                    }
                    else
                    {
                        // Ч�ʱȲ�����Ҫ����ɵ�ǰ�鲢��ʼ�¿�
                        currentBlock.EfficiencyRatio = CalculateEfficiencyRatio(currentBlock);
                        optimizedBlocks.Add(currentBlock);
                        
                        currentBlock = new S7AddressBlock
                        {
                            DbNumber = dbGroup.Key.DbNumber,
                            StartByte = address.StartByte,
                            TotalLength = address.Length,
                            Addresses = new List<S7AddressInfo> { address }
                        };
                    }
                }
                
                // ������һ����
                if (currentBlock.Addresses.Count > 0)
                {
                    currentBlock.EfficiencyRatio = CalculateEfficiencyRatio(currentBlock);
                    optimizedBlocks.Add(currentBlock);
                }
            }
            
            return optimizedBlocks;
        }

        /// <summary>
        /// �����ַ���Ч�ʱ�
        /// </summary>
        /// <param name="block">��ַ��</param>
        /// <returns>Ч�ʱȣ�0-1֮�䣩</returns>
        private double CalculateEfficiencyRatio(S7AddressBlock block)
        {
            if (block.TotalLength == 0) return 0;
            
            var effectiveDataLength = block.Addresses.Sum(a => a.Length);
            return (double)effectiveDataLength / block.TotalLength;
        }

        /// <summary>
        /// ִ��������ȡ����
        /// </summary>
        /// <param name="blocks">�Ż���ĵ�ַ���б�</param>
        /// <returns>��ȡ�������Ϊ���ʶ��ֵΪ��ȡ���ֽ�����</returns>
        private async Task<OperationResult<Dictionary<string, byte[]>>> ExecuteBatchRead(List<S7AddressBlock> blocks)
        {
            var result = new OperationResult<Dictionary<string, byte[]>>();
            var blockDataDict = new Dictionary<string, byte[]>();
            var errors = new List<string>();

            foreach (var block in blocks)
            {
                try
                {
                    // �����ĵ�ַ�ַ��������ݵ�ַ����ѡ����ʵĸ�ʽ
                    string blockAddress = "";
                    string blockKey = "";
                    
                    if (block.Addresses.Count > 0)
                    {
                        var firstAddress = block.Addresses[0];
                        var areaType = GetAreaType(firstAddress.DataType);
                        
                        switch (areaType)
                        {
                            case "DB":
                                blockAddress = $"DB{block.DbNumber}.DBB{block.StartByte}";
                                blockKey = $"DB{block.DbNumber}_{block.StartByte}_{block.TotalLength}";
                                break;
                            case "I":
                                blockAddress = $"I{block.StartByte}";
                                blockKey = $"I_{block.StartByte}_{block.TotalLength}";
                                break;
                            default:
                                errors.Add($"��֧�ֵĵ�ַ����: {areaType}");
                                continue;
                        }
                    }
                    else
                    {
                        errors.Add($"����û�е�ַ��Ϣ");
                        continue;
                    }
                    
                    // ֱ�ӵ��õײ��ȡ�����������ظ�����
                    var readResult = await InternalReadAsync(blockAddress, block.TotalLength, false);
                    
                    if (readResult.IsSuccess)
                    {
                        blockDataDict[blockKey] = readResult.ResultValue;
                        
                        // �ϲ��������Ӧ��־
                        result.Requsts.AddRange(readResult.Requsts);
                        result.Responses.AddRange(readResult.Responses);
                    }
                    else
                    {
                        var areaType = GetAreaType(block.Addresses[0].DataType);
                        errors.Add($"��ȡ�� {areaType}{(areaType == "DB" ? block.DbNumber.ToString() : "")}:{block.StartByte}-{block.StartByte + block.TotalLength - 1} ʧ��: {readResult.Message}");
                    }
                }
                catch (Exception ex)
                {
                    var areaType = block.Addresses.Count > 0 ? GetAreaType(block.Addresses[0].DataType) : "UNKNOWN";
                    errors.Add($"��ȡ�� {areaType}{(areaType == "DB" ? block.DbNumber.ToString() : "")}:{block.StartByte}-{block.StartByte + block.TotalLength - 1} �쳣: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                result.IsSuccess = blockDataDict.Count > 0; // ���ֳɹ�
                result.Message = string.Join("; ", errors);
            }
            else
            {
                result.IsSuccess = true;
            }

            result.ResultValue = blockDataDict;
            return result.Complete();
        }

        /// <summary>
        /// �ڲ���ȡ����������������������ȡʹ��
        /// </summary>
        private async ValueTask<OperationResult<byte[]>> InternalReadAsync(string address, int length, bool isBit = false)
        {
            if (Transport is S7EthernetTransport s7Transport)
            {
                var tempResult = new OperationResult<byte>();
                var readRequest = new S7ReadRequest(address, 0, length, isBit);
                var response = await s7Transport.UnicastReadMessageAsync(readRequest);
                if (response.IsSuccess)
                {
                    int realLength = length;
                    var dataPackage = response.ResultValue.ProtocolMessageFrame;
                    byte[] responseData = new byte[realLength];
                    try
                    {
                        //0x04 �� 0x01 ��ȡһ������ //�����������ȡ��������ȡ������������֤
                        if (dataPackage[19] == 0x04 && dataPackage[20] == 0x01)
                        {
                            if (dataPackage[21] == 0x0A && dataPackage[22] == 0x00)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"��ȡ{address}ʧ�ܣ���ȷ���Ƿ���ڵ�ַ{address}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                            else if (dataPackage[21] == 0x05 && dataPackage[22] == 0x00)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"��ȡ{address}ʧ�ܣ���ȷ���Ƿ���ڵ�ַ{address}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                            else if (dataPackage[21] != 0xFF)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"��ȡ{address}ʧ�ܣ��쳣����[{21}]:{dataPackage[21]}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                        }
                        if (isBit) { realLength = (int)(Math.Ceiling(realLength / 8.0)); }
                        Array.Copy(dataPackage, dataPackage.Length - realLength, responseData, 0, realLength);
                    }
                    catch (Exception ex)
                    {
                        tempResult.Exception = ex;
                        tempResult.Message = $"{address} 0 {length} ��ȡԤ�ڳ����뷵�����ݳ��Ȳ�һ��";
                        return OperationResult.CreateFailedResult<byte[]>(tempResult);
                    }
                    
                    var result = new OperationResult<byte[]>(response, responseData);
                    result.Requsts.Add(string.Join(" ", readRequest.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
                    result.Responses.Add(string.Join(" ", response.ResultValue.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
                    return result.Complete();
                }
                else
                {
                    return OperationResult.CreateFailedResult<byte[]>(response);
                }
            }
            return OperationResult.CreateFailedResult<byte[]>();
        }

        /// <summary>
        /// �Ӷ�ȡ�Ŀ���������ȡ������ַ��Ӧ������
        /// </summary>
        /// <param name="blockData">�������ֵ�</param>
        /// <param name="blocks">��ַ����Ϣ</param>
        /// <param name="originalAddresses">ԭʼ��ַ��Ϣ</param>
        /// <returns>��ַ������ֵ��ӳ��</returns>
        private Dictionary<string, object> ExtractDataFromBlocks(Dictionary<string, byte[]> blockData, List<S7AddressBlock> blocks, List<S7AddressInfo> originalAddresses)
        {
            var result = new Dictionary<string, object>();

            foreach (var block in blocks)
            {
                // ���ݵ�ַ����������ȷ�Ŀ��
                string blockKey = "";
                if (block.Addresses.Count > 0)
                {
                    var firstAddress = block.Addresses[0];
                    var areaType = GetAreaType(firstAddress.DataType);
                    
                    switch (areaType)
                    {
                        case "DB":
                            blockKey = $"DB{block.DbNumber}_{block.StartByte}_{block.TotalLength}";
                            break;
                        case "I":
                            blockKey = $"I_{block.StartByte}_{block.TotalLength}";
                            break;
                        default:
                            // �޷�ʶ��ĵ�ַ���ͣ�����
                            foreach (var address in block.Addresses)
                            {
                                result[address.OriginalAddress] = null;
                            }
                            continue;
                    }
                }
                else
                {
                    // ����û�е�ַ������
                    continue;
                }
                
                if (!blockData.TryGetValue(blockKey, out byte[] data))
                {
                    // �ÿ��ȡʧ�ܣ�������������е�ַ���Ϊnull
                    foreach (var address in block.Addresses)
                    {
                        result[address.OriginalAddress] = null;
                    }
                    continue;
                }

                // �ӿ���������ȡ������ַ��ֵ
                foreach (var address in block.Addresses)
                {
                    try
                    {
                        var relativeOffset = address.StartByte - block.StartByte;
                        
                        if (relativeOffset < 0 || relativeOffset + address.Length > data.Length)
                        {
                            result[address.OriginalAddress] = null;
                            continue;
                        }

                        object value = ExtractValueFromBytes(data, relativeOffset, address);
                        result[address.OriginalAddress] = value;
                    }
                    catch (Exception)
                    {
                        result[address.OriginalAddress] = null;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// ���ֽ���������ȡָ�����͵�ֵ
        /// </summary>
        /// <param name="data">�ֽ�����</param>
        /// <param name="offset">ƫ����</param>
        /// <param name="addressInfo">��ַ��Ϣ</param>
        /// <returns>��ȡ��ֵ</returns>
        private object ExtractValueFromBytes(byte[] data, int offset, S7AddressInfo addressInfo)
        {
            switch (addressInfo.DataType)
            {
                case S7DataType.DBX:
                    // λ���ݣ�����I����Q����λ��ַ��
                    if (offset < data.Length)
                    {
                        var byteValue = data[offset];
                        return (byteValue & (1 << addressInfo.BitOffset)) != 0;
                    }
                    return false;

                case S7DataType.DBB:
                case S7DataType.IB:
                    // �ֽ�����
                    if (offset < data.Length)
                    {
                        return data[offset];
                    }
                    return (byte)0;

                case S7DataType.DBW:
                case S7DataType.IW:
                    // ������ (2�ֽ�)
                    if (offset + 1 < data.Length)
                    {
                        if (IsReverse)
                        {
                            return (ushort)(data[offset] << 8 | data[offset + 1]);
                        }
                        else
                        {
                            return (ushort)(data[offset + 1] << 8 | data[offset]);
                        }
                    }
                    return (ushort)0;

                case S7DataType.DBD:
                case S7DataType.ID:
                    // ˫������ (4�ֽ�)
                    if (offset + 3 < data.Length)
                    {
                        if (IsReverse)
                        {
                            return (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);
                        }
                        else
                        {
                            return (uint)(data[offset + 3] << 24 | data[offset + 2] << 16 | data[offset + 1] << 8 | data[offset]);
                        }
                    }
                    return (uint)0;

                default:
                    return null;
            }
        }


        public override async Task<OperationResult<Dictionary<string, object>>> BatchReadAsync(Dictionary<string, object> addresses)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult<Dictionary<string, object>>();
                
                try
                {
                    // ������֤
                    if (addresses == null || addresses.Count == 0)
                    {
                        result.ResultValue = new Dictionary<string, object>();
                        return result.Complete();
                    }

                    // ����1: ������ַ
                    var addressInfos = ParseAddresses(addresses);
                    if (addressInfos.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "���е�ַ����ʧ��";
                        result.ResultValue = new Dictionary<string, object>();
                        return result.Complete();
                    }

                    // ����2: �Ż���ַ��
                    var optimizedBlocks = OptimizeAddressBlocks(addressInfos);
                    if (optimizedBlocks.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "��ַ�Ż�ʧ��";
                        result.ResultValue = new Dictionary<string, object>();
                        return result.Complete();
                    }

                    // ����3: ִ��������ȡ
                    var batchReadResult = await ExecuteBatchRead(optimizedBlocks);
                    
                    // �ϲ�������Ӧ��־
                    result.Requsts.AddRange(batchReadResult.Requsts);
                    result.Responses.AddRange(batchReadResult.Responses);

                    if (!batchReadResult.IsSuccess)
                    {
                        result.IsSuccess = false;
                        result.Message = batchReadResult.Message;
                        result.ResultValue = new Dictionary<string, object>();
                        return result.Complete();
                    }

                    // ����4: ��ȡ����ַ������
                    var extractedData = ExtractDataFromBlocks(batchReadResult.ResultValue, optimizedBlocks, addressInfos);
                    
                    result.ResultValue = extractedData;
                    result.IsSuccess = true;
                    
                    // ͳ����Ϣ
                    var totalAddresses = addresses.Count;
                    var successfulAddresses = extractedData.Count(kvp => kvp.Value != null);
                    var totalBlocks = optimizedBlocks.Count;
                    
                    if (successfulAddresses < totalAddresses)
                    {
                        result.Message = $"������ȡ���ֳɹ�: {successfulAddresses}/{totalAddresses} ����ַ�ɹ���ʹ���� {totalBlocks} ���Ż���";
                    }
                    else
                    {
                        result.Message = $"������ȡ�ɹ�: {successfulAddresses} ����ַ��ʹ���� {totalBlocks} ���Ż���";
                    }
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = $"������ȡ�쳣: {ex.Message}";
                    result.Exception = ex;
                    result.ResultValue = new Dictionary<string, object>();
                }

                return result.Complete();
            }
        }

        public override async Task<OperationResult> BatchWriteAsync(Dictionary<string, object> addresses)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                var errors = new List<string>();
                var successCount = 0;
                
                try
                {
                    // ������֤
                    if (addresses == null || addresses.Count == 0)
                    {
                        return result.Complete();
                    }

                    // ����д����ʱ�������д��ķ�ʽ
                    // ��Ϊд��������Ż��ȶ�ȡ�����ӣ���Ҫ�������ݸ�������
                    foreach (var kvp in addresses)
                    {
                        try
                        {
                            var address = kvp.Key;
                            var value = kvp.Value;
                            
                            // ��ֵת��Ϊ�ֽ�����
                            byte[] data = ConvertValueToBytes(value, address);
                            if (data == null)
                            {
                                errors.Add($"��ַ {address} ��ֵת��ʧ��");
                                continue;
                            }

                            // ִ��д��
                            var writeResult = await WriteAsync(address, data, false);
                            
                            // �ϲ�������Ӧ��־
                            result.Requsts.AddRange(writeResult.Requsts);
                            result.Responses.AddRange(writeResult.Responses);
                            
                            if (writeResult.IsSuccess)
                            {
                                successCount++;
                            }
                            else
                            {
                                errors.Add($"д���ַ {address} ʧ��: {writeResult.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"д���ַ {kvp.Key} �쳣: {ex.Message}");
                        }
                    }

                    // ���ý��״̬
                    var totalCount = addresses.Count;
                    if (successCount == totalCount)
                    {
                        result.IsSuccess = true;
                        result.Message = $"����д��ɹ�: {successCount} ����ַ";
                    }
                    else if (successCount > 0)
                    {
                        result.IsSuccess = true;
                        result.Message = $"����д�벿�ֳɹ�: {successCount}/{totalCount} ����ַ�ɹ�";
                        if (errors.Count > 0)
                        {
                            result.Message += $"������: {string.Join("; ", errors)}";
                        }
                    }
                    else
                    {
                        result.IsSuccess = false;
                        result.Message = $"����д��ʧ��: {string.Join("; ", errors)}";
                    }
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = $"����д���쳣: {ex.Message}";
                    result.Exception = ex;
                }

                return result.Complete();
            }
        }

        /// <summary>
        /// ���ݵ�ַ���ͽ�ֵת��Ϊ�ֽ�����
        /// </summary>
        /// <param name="value">Ҫд���ֵ</param>
        /// <param name="address">Ŀ���ַ</param>
        /// <returns>ת������ֽ�����</returns>
        private byte[] ConvertValueToBytes(object value, string address)
        {
            try
            {
                // ������ַ��ȷ����������
                var addressInfo = ParseSingleAddress(address);
                
                switch (addressInfo.DataType)
                {
                    case S7DataType.DBX:
                    // I����Q��λ��ַ��ʹ��DBX��ʾ
                    
                    case S7DataType.MX:
                        // λ����
                        if (value is bool boolValue)
                        {
                            return new byte[] { (byte)(boolValue ? 1 : 0) };
                        }
                        else if (value is int intValue)
                        {
                            return new byte[] { (byte)(intValue != 0 ? 1 : 0) };
                        }
                        break;

                    case S7DataType.DBB:
                    case S7DataType.IB:
                    case S7DataType.QB:
                    case S7DataType.MB:
                    case S7DataType.VB:
                        // �ֽ�����
                        if (value is byte byteValue)
                        {
                            return new byte[] { byteValue };
                        }
                        else if (value is int intValue && intValue >= 0 && intValue <= 255)
                        {
                            return new byte[] { (byte)intValue };
                        }
                        break;

                    case S7DataType.DBW:
                    case S7DataType.IW:
                    case S7DataType.QW:
                    case S7DataType.MW:
                    case S7DataType.VW:
                        // ������
                        ushort ushortValue = 0;
                        if (value is ushort us)
                            ushortValue = us;
                        else if (value is int intValue && intValue >= 0 && intValue <= 65535)
                            ushortValue = (ushort)intValue;
                        else if (value is short shortValue && shortValue >= 0)
                            ushortValue = (ushort)shortValue;
                        else
                            break;

                        if (IsReverse)
                        {
                            return new byte[] { (byte)(ushortValue >> 8), (byte)(ushortValue & 0xFF) };
                        }
                        else
                        {
                            return new byte[] { (byte)(ushortValue & 0xFF), (byte)(ushortValue >> 8) };
                        }

                    case S7DataType.DBD:
                    case S7DataType.ID:
                    case S7DataType.QD:
                    case S7DataType.MD:
                    case S7DataType.VD:
                        // ˫������
                        uint uintValue = 0;
                        if (value is uint ui)
                            uintValue = ui;
                        else if (value is int intValue && intValue >= 0)
                            uintValue = (uint)intValue;
                        else if (value is long longValue && longValue >= 0 && longValue <= uint.MaxValue)
                            uintValue = (uint)longValue;
                        else if (value is float floatValue)
                        {
                            // ��floatת��Ϊuint
                            var floatBytes = BitConverter.GetBytes(floatValue);
                            if (BitConverter.IsLittleEndian && IsReverse)
                            {
                                Array.Reverse(floatBytes);
                            }
                            return floatBytes;
                        }
                        else
                            break;

                        if (IsReverse)
                        {
                            return new byte[] 
                            { 
                                (byte)(uintValue >> 24), 
                                (byte)((uintValue >> 16) & 0xFF), 
                                (byte)((uintValue >> 8) & 0xFF), 
                                (byte)(uintValue & 0xFF) 
                            };
                        }
                        else
                        {
                            return new byte[] 
                            { 
                                (byte)(uintValue & 0xFF), 
                                (byte)((uintValue >> 8) & 0xFF), 
                                (byte)((uintValue >> 16) & 0xFF), 
                                (byte)(uintValue >> 24) 
                            };
                        }
                }
            }
            catch (Exception)
            {
                // ��ַ������ת��ʧ��
            }

            return null;
        } 

    }
}
