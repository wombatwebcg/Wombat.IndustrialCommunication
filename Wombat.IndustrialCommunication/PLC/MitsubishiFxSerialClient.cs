using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication.PLC
{
    public class MitsubishiFxSerialClient : PLCSerialPortBase
    {
        private AsyncLock _lock;


        public MitsubishiFxSerialClient(string portName, int baudRate = 9600, int dataBits = 7, StopBits stopBits = StopBits.One, Parity parity = Parity.Even, Handshake handshake = Handshake.None)
        {
            _lock = new AsyncLock();
            IsReverse = false;
            DataFormat = EndianFormat.DCBA;

            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            Handshake = handshake;
            Parity = parity;
            StopBits = stopBits;
        }

        public MitsubishiFxSerialClient()
        {
            _lock = new AsyncLock();
            DataFormat = EndianFormat.DCBA;

        }

        public override string Version => "FXProgramPort";

        public override OperationResult<byte[]> Read(string address, int length, bool isBit = false)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            throw new NotImplementedException();
        }

        public override OperationResult Write(string address, byte[] data, bool isBit = false)
        {
            using (_lock.Lock())
            {
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        return connectResult.Complete();
                    }
                }
                OperationResult result = new OperationResult();
                try
                {
                    //发送写入信息
                    MitsubishiMCAddress arg = null;
                    byte[] command = null;
                    arg = ConvertArgFx(address);
                    command = GetWriteCommand(arg.BeginAddress,arg.TypeCode,data,isBit);
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));

                    OperationResult<byte[]> sendResult = new OperationResult<byte[]>();
                    sendResult = InterpretAndExtractMessageData(command);
                    if (!sendResult.IsSuccess)
                    {
                        return sendResult;
                    }
                    byte[] dataPackage = sendResult.Value;
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                    result.Exception = ex;
                    _serialPort?.Close();
                }

                finally
                {
                    if (!IsUseLongConnect) Disconnect();
                }
                return result.Complete();
            }
        }

        public override Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
        {
            throw new NotImplementedException();
        }



        /// <summary>
        /// FX地址解析
        /// </summary>
        /// <param name="address"></param>
        /// <param name="toUpper"></param>
        /// <returns></returns>
        private MitsubishiMCAddress ConvertArgFx(string address, bool toUpper = true)
        {
            if (toUpper) address = address.ToUpper();
            var addressInfo = new MitsubishiMCAddress();
            switch (address[0])
            {
                case 'X'://X输入寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x9C, 0x20 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 8;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                        addressInfo.BeginAddress = (ushort)(addressInfo.BeginAddress + 0x0400);
                    }
                    break;
                case 'Y'://Y输出寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x9D, 0x20 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 8;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                        addressInfo.BeginAddress = (ushort)(addressInfo.BeginAddress + 0x0500);
                    }
                    break;
                case 'M'://M中间寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x90, 0x20 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                        if (addressInfo.BeginAddress >= 8000)
                        {
                            addressInfo.BeginAddress = (ushort)((addressInfo.BeginAddress - 8000) / 8 + 0x01E0);
                        }
                        else
                        {
                            addressInfo.BeginAddress = (ushort)(addressInfo.BeginAddress / 8 + 0x0100);
                        }

                    }
                    break;
                case 'S'://S状态寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x98, 0x20 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'D'://D数据寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0xA8, 0x20 };
                        addressInfo.BitType = 0x00;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                        if (addressInfo.BeginAddress >= 8000)
                        {
                            addressInfo.BeginAddress = (ushort)((addressInfo.BeginAddress - 8000) * 2 + 0x0E00);
                        }
                        else
                        {
                            addressInfo.BeginAddress = (ushort)(addressInfo.BeginAddress * 2 + 0x1000);
                        }

                    }
                    break;
                case 'R'://R文件寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0xAF, 0x20 };
                        addressInfo.BitType = 0x00;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
            }
            return addressInfo;
        }




        private byte[] GetWriteCommand(int beginAddress, byte[] typeCode, byte[] data, bool isBit)
        {
            byte[] _PLCCommand = new byte[9];
            var length = data.Length / 2;
            byte[] startAddressBytes = beginAddress.ToByte(format:EndianFormat.CDAB);
            if (isBit)
            {
                length = data.Length < 2 ? 1 : data.Length * 2;
                if (length == 1)
                {
                    _PLCCommand[0] = 0x02;                         // STX
                    _PLCCommand[1] = data[0] > 0 ? (byte)0x37 : (byte)0x38;  // Read
                    _PLCCommand[2] = Encoding.ASCII.GetBytes(beginAddress.ToString("X4"))[2];         // 偏移地址
                    _PLCCommand[3] = Encoding.ASCII.GetBytes(beginAddress.ToString("X4"))[3];
                    _PLCCommand[4] = Encoding.ASCII.GetBytes(beginAddress.ToString("X4"))[0];
                    _PLCCommand[5] = Encoding.ASCII.GetBytes(beginAddress.ToString("X4"))[1];
                    _PLCCommand[6] = 0x03;                                      // ETX
                    FxCalculateCRC(_PLCCommand).CopyTo(_PLCCommand, 7);         // CRC
                }
            }
            else
            {

            }
            return _PLCCommand;


        }

        /// <summary>
        /// 返回读取的地址及长度信息
        /// </summary>
        /// <param name="address">读取的地址信息</param>
        /// <returns>带起始地址的结果对象</returns>
        private static OperationResult<ushort> FxCalculateWordStartAddress(string address)
        {
            // 初步解析，失败就返回
            var analysis = FxAnalysisAddress(address);
            if (!analysis.IsSuccess) return OperationResult.CreateFailedResult<ushort>(analysis);

            // 二次解析
            ushort startAddress = analysis.Content2;
            if (analysis.Content1 == MelsecMcDataType.D)
            {
                if (startAddress >= 8000)
                {
                    startAddress = (ushort)((startAddress - 8000) * 2 + 0x0E00);
                }
                else
                {
                    startAddress = (ushort)(startAddress * 2 + 0x1000);
                }
            }
            else if (analysis.Content1 == MelsecMcDataType.CN)
            {
                if (startAddress >= 200)
                {
                    startAddress = (ushort)((startAddress - 200) * 4 + 0x0C00);
                }
                else
                {
                    startAddress = (ushort)(startAddress * 2 + 0x0A00);
                }
            }
            else if (analysis.Content1 == MelsecMcDataType.TN)
            {
                startAddress = (ushort)(startAddress * 2 + 0x0800);
            }
            else
            {
                return new OperationResult<ushort>(StringResources.Language.MelsecCurrentTypeNotSupportedWordOperate);
            }

            return OperationResult.CreateSuccessResult(startAddress);
        }

        /// <summary>
        /// 返回读取的地址及长度信息，以及当前的偏置信息
        /// </summary><param name="address">读取的地址信息</param>
        /// <returns>带起始地址的结果对象</returns>
        private static OperationResult<ushort, ushort, ushort> FxCalculateBoolStartAddress(string address)
        {
            // 初步解析
            var analysis = FxAnalysisAddress(address);
            if (!analysis.IsSuccess) return OperationResult.CreateFailedResult<ushort, ushort, ushort>(analysis);

            // 二次解析
            ushort startAddress = analysis.Content2;
            if (analysis.Content1 == MelsecMcDataType.M)
            {
                if (startAddress >= 8000)
                {
                    startAddress = (ushort)((startAddress - 8000) / 8 + 0x01E0);
                }
                else
                {
                    startAddress = (ushort)(startAddress / 8 + 0x0100);
                }
            }
            else if (analysis.Content1 == MelsecMcDataType.X)
            {
                startAddress = (ushort)(startAddress / 8 + 0x0080);
            }
            else if (analysis.Content1 == MelsecMcDataType.Y)
            {
                startAddress = (ushort)(startAddress / 8 + 0x00A0);
            }
            else if (analysis.Content1 == MelsecMcDataType.S)
            {
                startAddress = (ushort)(startAddress / 8 + 0x0000);
            }
            else if (analysis.Content1 == MelsecMcDataType.CS)
            {
                startAddress += (ushort)(startAddress / 8 + 0x01C0);
            }
            else if (analysis.Content1 == MelsecMcDataType.CC)
            {
                startAddress += (ushort)(startAddress / 8 + 0x03C0);
            }
            else if (analysis.Content1 == MelsecMcDataType.TS)
            {
                startAddress += (ushort)(startAddress / 8 + 0x00C0);
            }
            else if (analysis.Content1 == MelsecMcDataType.TC)
            {
                startAddress += (ushort)(startAddress / 8 + 0x02C0);
            }
            else
            {
                return new OperationResult<ushort, ushort, ushort>(StringResources.Language.MelsecCurrentTypeNotSupportedBitOperate);
            }

            return OperationResult.CreateSuccessResult(startAddress, analysis.Content2, (ushort)(analysis.Content2 % 8));
        }


        /// <summary>
        /// 计算Fx协议指令的和校验信息
        /// </summary>
        /// <param name="data">字节数据</param>
        /// <returns>校验之后的数据</returns>
        internal static byte[] FxCalculateCRC(byte[] data)
        {
            int sum = 0;
            for (int i = 1; i < data.Length - 2; i++)
            {
                sum += data[i];
            }
            return Encoding.ASCII.GetBytes(((byte)sum).ToString("X2")); 
        }

        internal OperationResult CheckResponse(byte[] ack,bool isCheckCrc)
        {
            if (ack.Length == 0) return OperationResult.CreateFailedResult(StringResources.Language.MelsecFxReceiveZore);
            if (ack[0] == 0x15)  return OperationResult.CreateFailedResult(StringResources.Language.MelsecFxAckNagative + " Actual: " + ack.ToHexString());
            if (ack[0] != 0x02)  return OperationResult.CreateFailedResult(StringResources.Language.MelsecFxAckWrong + ack[0] + " Actual: " + ack.ToHexString());

            if(isCheckCrc) if (!checkCRC(ack)) return OperationResult.CreateFailedResult(StringResources.Language.MelsecFxCrcCheckFailed);
            return OperationResult.CreateSuccessResult();

            bool checkCRC(byte[] data)
            {
                byte[] crc = FxCalculateCRC(data);
                if (crc[0] != data[data.Length - 2]) return false;
                if (crc[1] != data[data.Length - 1]) return false;
                return true;
            }
        }

    }
}
