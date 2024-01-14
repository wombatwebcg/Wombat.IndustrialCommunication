using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 三菱PLC编程串口(rs232)通讯协议(一次最好别读超过10个byte)
    /// </summary>
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

        public override OperationResult<bool> ReadBoolean(string address)
        {
            var readResut = Read(address, 1, isBit: true);
            var result = new OperationResult<bool>(readResut);
            if (result.IsSuccess)
            {
                int offest = int.Parse(address.Substring(1)) % 8;
                result.Value = readResut.Value.ToBool(0, 8)[offest];
            }

            return result.Complete();
        }

        public override async ValueTask<OperationResult<bool>> ReadBooleanAsync(string address)
        {
            var readResut =await ReadAsync(address, 1, isBit: true);
            var result = new OperationResult<bool>(readResut);
            if (result.IsSuccess)
            {
                int offest = int.Parse(address.Substring(1)) % 8;
                result.Value = readResut.Value.ToBool(0, 8)[offest];
            }

            return result.Complete();

        }

        public override OperationResult<bool[]> ReadBoolean(string address, int length)
        {
            var readResut = Read(address, length, isBit: true);
            var result = new OperationResult<bool[]>(readResut);
            if (result.IsSuccess)
            {
                int index = int.Parse(address.Substring(1))%8;
                result.Value = new bool[length];
                bool[] resultSet = readResut.Value.ToBool(0, readResut.Value.Length * 8);
                Array.Copy(resultSet, index, result.Value, 0, length);
            }
            return result.Complete();

        }

        public override async ValueTask<OperationResult<bool[]>> ReadBooleanAsync(string address, int length)
        {
            var readResut = await ReadAsync(address, length, isBit: true);
            var result = new OperationResult<bool[]>(readResut);
            if (result.IsSuccess)
            {
                int index = int.Parse(address.Substring(1)) % 8;
                result.Value = new bool[length];
                bool[] resultSet = readResut.Value.ToBool(0, readResut.Value.Length * 8);
                Array.Copy(resultSet, index, result.Value, 0, length);
            }

            return result.Complete();

        }


        [Obsolete(("慎用,由单个字写入封装"))]
        public override  OperationResult Write(string address, bool[] value)
        {
            var args = ConvertArgFx(address);
            for (int i = 0; i < value.Length; i++)
            {
                var result = Write($"{args.TypeChar}{Convert.ToString(args.BeginAddress + i, args.Format)}", value[i]).Complete();
                if (result.IsSuccess)
                {
                    continue;
                }
                else
                {
                    return result;
                }
            }
            return OperationResult.CreateSuccessResult();


            //throw new Exception("暂不支持");

        }

        [Obsolete(("慎用,由单个字写入封装"))]
        public override async Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            var args = ConvertArgFx(address);
            for (int i = 0; i < value.Length; i++)
            {
                var result =await WriteAsync($"{args.TypeChar}{Convert.ToString(args.BeginAddress + i, args.Format)}", value[i]);
                if (result.IsSuccess)
                {
                    continue;
                }
                else
                {
                    return result.Complete();
                }
            }
            return OperationResult.CreateSuccessResult();

            //throw new Exception("暂不支持");

        }



        public override OperationResult<byte[]> Read(string address, int length, bool isBit = false)
        {
            using (_lock.Lock())
            {
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取{address}失败，{ connectResult.Message}";
                        return new OperationResult<byte[]>(connectResult).Complete();
                    }

                }
                var result = new OperationResult<byte[]>() {IsSuccess = false };
                try
                {
                    //发送读取信息
                    MitsubishiMCAddress arg = null;
                    byte[] command = null;

                    arg = ConvertArgFx(address);
                    command = GetReadCommand(arg.BeginAddress, arg.TypeChar, (ushort)length, isBit);
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));

                    OperationResult<byte[]> sendResult = new OperationResult<byte[]>();
                    sendResult = InterpretAndExtractMessageData(command);
                    if (!sendResult.IsSuccess) return sendResult;
                    byte[] dataPackage = sendResult.Value;
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
                    if (!CheckReadResponse(dataPackage).IsSuccess) { return result; }

                    var bufferLength = length;
                    byte[] responseValue = new byte[(dataPackage.Length - 4) / 2];
                    for (int i = 0; i < responseValue.Length; i++)
                    {
                        byte[] buffer = new byte[2];
                        buffer[0] = dataPackage[i * 2 + 1];
                        buffer[1] = dataPackage[i * 2 + 2];

                        responseValue[i] = Convert.ToByte(Encoding.ASCII.GetString(buffer), 16);
                    }
                    result.Value = responseValue;
                    result.IsSuccess = true;
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

        public override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取{address}失败，{ connectResult.Message}";
                        return new OperationResult<byte[]>(connectResult).Complete();
                    }

                }
                var result = new OperationResult<byte[]>() { IsSuccess = false };
                try
                {
                    //发送读取信息
                    MitsubishiMCAddress arg = null;
                    byte[] command = null;

                    arg = ConvertArgFx(address);
                    command = GetReadCommand(arg.BeginAddress, arg.TypeChar, (ushort)length, isBit);
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));

                    OperationResult<byte[]> sendResult = new OperationResult<byte[]>();
                    sendResult = await InterpretAndExtractMessageDataAsync(command);
                    if (!sendResult.IsSuccess) return sendResult;
                    byte[] dataPackage = sendResult.Value;
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
                    if (!CheckReadResponse(dataPackage).IsSuccess) { return result; }

                    var bufferLength = length;
                    byte[] responseValue = new byte[(dataPackage.Length - 4) / 2];
                    for (int i = 0; i < responseValue.Length; i++)
                    {
                        byte[] buffer = new byte[2];
                        buffer[0] = dataPackage[i * 2 + 1];
                        buffer[1] = dataPackage[i * 2 + 2];

                        responseValue[i] = Convert.ToByte(Encoding.ASCII.GetString(buffer), 16);
                    }
                    result.Value = responseValue;
                    result.IsSuccess = true;
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
                    if (!IsUseLongConnect) await DisconnectAsync();
                }
                return result.Complete();
            }
        }

        public override  OperationResult Write(string address, byte[] data, bool isBit = false)
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
                OperationResult result = new OperationResult() {IsSuccess = false };
                try
                {
                    //发送写入信息
                    MitsubishiMCAddress arg = null;
                    byte[] command = null;
                    arg = ConvertArgFx(address);
                    command = GetWriteCommand(arg.BeginAddress,arg.TypeChar,data,isBit);
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));
                    OperationResult<byte[]> sendResult = new OperationResult<byte[]>() { IsSuccess = false };
                    sendResult = InterpretAndExtractMessageData(command);
                    if (!sendResult.IsSuccess)
                    {
                        return sendResult;
                    }
                    byte[] dataPackage = sendResult.Value;
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
                    if(CheckWriteResponse(dataPackage).IsSuccess)
                    {
                        result.IsSuccess = true;
                        return result.Complete();
                    }
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

        public override async Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        return connectResult.Complete();
                    }
                }
                OperationResult result = new OperationResult() { IsSuccess = false };
                try
                {
                    //发送写入信息
                    MitsubishiMCAddress arg = null;
                    byte[] command = null;
                    arg = ConvertArgFx(address);
                    command = GetWriteCommand(arg.BeginAddress, arg.TypeChar, data, isBit);
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));
                    OperationResult<byte[]> sendResult = new OperationResult<byte[]>() { IsSuccess = false };
                    sendResult =await InterpretAndExtractMessageDataAsync(command);
                    if (!sendResult.IsSuccess)
                    {
                        return sendResult;
                    }
                    byte[] dataPackage = sendResult.Value;
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
                    if (CheckWriteResponse(dataPackage).IsSuccess)
                    {
                        result.IsSuccess = true;
                        return result.Complete();
                    }
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
                    if (!IsUseLongConnect) await DisconnectAsync();
                }
                return result.Complete();
            }
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
                        addressInfo.TypeCode = new byte[] { 0x9C };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 8;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'Y'://Y输出寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x9D };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 8;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'M'://M中间寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x90 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'S'://S状态寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x98 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'D'://D数据寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0xA8 };
                        addressInfo.BitType = 0x00;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'R'://R文件寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0xAF };
                        addressInfo.BitType = 0x00;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
            }
            return addressInfo;
        }



        /// <summary>
        /// 获取A_1E读取命令
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="typeCode"></param>
        /// <param name="length"></param>
        /// <param name="isBit"></param>
        /// <returns></returns>
        protected byte[] GetReadCommand(int beginAddress, string typeChar, ushort length, bool isBit)
        {
            ushort factLength = 1;
            switch (typeChar)
            {
                case "X": beginAddress = (ushort)(beginAddress / 8 + 0x0080); break;
                case "Y": beginAddress = (ushort)(beginAddress / 8 + 0x00A0); break;
                case "M"://M中间寄存器
                    {
                        if (beginAddress >= 8000)
                        {
                            beginAddress = (ushort)((beginAddress - 8000) / 8 + 0x01E0);
                        }
                        else
                        {
                            beginAddress = (ushort)(beginAddress / 8 + 0x0100);
                        }
                    }
                    break;
                case "S": beginAddress = (ushort)(beginAddress / 8 + 0x0000); break;
                case "D"://D数据寄存器
                    if (beginAddress >= 8000)
                    {
                        beginAddress = (ushort)((beginAddress - 8000) * 2 + 0x0E00);
                    }
                    else
                    {
                        beginAddress = (ushort)(beginAddress * 2 + 0x1000);
                    }
                    break;
            }
            byte[] address = address = Encoding.ASCII.GetBytes(beginAddress.ToString("X4"));
            byte[] PLCCommand = new byte[11];
            if (isBit)
            {
                // 计算下实际需要读取的数据长度
                factLength = (ushort)((beginAddress + length - 1) / 8 - (beginAddress / 8) + 1);
                PLCCommand[0] = 0x02;                                                    // STX
                PLCCommand[1] = 0x30;                                                    // Read
                PLCCommand[2] = address[0];      // 偏移地址
                PLCCommand[3] = address[1];
                PLCCommand[4] = address[2];
                PLCCommand[5] = address[3];
                PLCCommand[6] = Encoding.ASCII.GetBytes(((byte)factLength).ToString("X2"))[0];          // Read Length
                PLCCommand[7] = Encoding.ASCII.GetBytes(((byte)factLength).ToString("X2"))[1];
                PLCCommand[8] = 0x03;                                                    // ETX
                FxCalculateCRC(PLCCommand).CopyTo(PLCCommand, 9);      // CRC
            }
            else
            {
                factLength = (ushort)(length);
                address = Encoding.ASCII.GetBytes(beginAddress.ToString("X4"));
                PLCCommand[0] = 0x02;                                                    // STX
                PLCCommand[1] = 0x30;                                                    // Read
                PLCCommand[2] = address[0];      // 偏移地址
                PLCCommand[3] = address[1];
                PLCCommand[4] = address[2];
                PLCCommand[5] = address[3];
                PLCCommand[6] = Encoding.ASCII.GetBytes(((byte)factLength).ToString("X2"))[0];          // Read Length
                PLCCommand[7] = Encoding.ASCII.GetBytes(((byte)factLength).ToString("X2"))[1];
                PLCCommand[8] = 0x03;                                                    // ETX
                FxCalculateCRC(PLCCommand).CopyTo(PLCCommand, 9);      // CRC
            }
            return PLCCommand;
        }

        private byte[] GetWriteCommand(int beginAddress, string typeChar, byte[] data, bool isBit)
        {
            byte[] PLCCommand = null;
            byte[] address = null;
            var length = data.Length / 2;
            if (isBit && data.Length == 1&&data[0]<=1)
            {
                switch (typeChar)
                {
                    case "X": beginAddress = (ushort)(beginAddress + 0x0400); break;
                    case "Y": beginAddress = (ushort)(beginAddress + 0x0500); break;
                    case "M"://M中间寄存器                           
                        if (beginAddress >= 8000)
                        {
                            beginAddress = (ushort)(beginAddress - 8000 + 0x0F00);
                        }
                        else
                        {
                            beginAddress = (ushort)(beginAddress + 0x0800);
                        }

                        break;
                    case "S": beginAddress = (ushort)(beginAddress + 0x0000); break;

                }
                address = Encoding.ASCII.GetBytes(beginAddress.ToString("X4"));
                PLCCommand = new byte[9];
                PLCCommand[0] = 0x02;                         // STX
                PLCCommand[1] = data[0] > 0 ? (byte)0x37 : (byte)0x38;  // Read
                PLCCommand[2] = address[2];         // 偏移地址
                PLCCommand[3] = address[3];
                PLCCommand[4] = address[0];
                PLCCommand[5] = address[1];
                PLCCommand[6] = 0x03;                                      // ETX
                FxCalculateCRC(PLCCommand).CopyTo(PLCCommand, 7);         // CRC
            }
            else
            {
                switch (typeChar)
                {
                    case "X": beginAddress = (ushort)(beginAddress / 8 + 0x0080); break;
                    case "Y": beginAddress = (ushort)(beginAddress / 8 + 0x00A0); break;
                    case "M"://M中间寄存器
                        {
                            if (beginAddress >= 8000)
                            {
                                beginAddress = (ushort)((beginAddress - 8000) / 8 + 0x01E0);
                            }
                            else
                            {
                                beginAddress = (ushort)(beginAddress / 8 + 0x0100);
                            }
                        }
                        break;
                    case "S": beginAddress = (ushort)(beginAddress / 8 + 0x0000); break;
                    case "D"://D数据寄存器
                        if (beginAddress >= 8000)
                        {
                            beginAddress = (ushort)((beginAddress - 8000) * 2 + 0x0E00);
                        }
                        else
                        {
                            beginAddress = (ushort)(beginAddress * 2 + 0x1000);
                        }
                        break;
                }
                address = Encoding.ASCII.GetBytes(beginAddress.ToString("X4"));
                byte[] buffer = new byte[data.Length * 2];
                if (data != null)
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        Encoding.ASCII.GetBytes(data[i].ToString("X2")).CopyTo(buffer, 2 * i);
                    }
                }
                var factLength = Encoding.ASCII.GetBytes(((byte)(buffer.Length / 2)).ToString("X2"));
                PLCCommand = new byte[11 + buffer.Length];
                PLCCommand[0] = 0x02;                                                                    // STX
                PLCCommand[1] = 0x31;                                                                    // Read
                PLCCommand[2] = address[0];                      // Offect Address
                PLCCommand[3] = address[1];
                PLCCommand[4] = address[2];
                PLCCommand[5] = address[3];
                PLCCommand[6] = factLength[0];          // Read Length
                PLCCommand[7] = factLength[1];
                Array.Copy(buffer, 0, PLCCommand, 8, buffer.Length);
                PLCCommand[PLCCommand.Length - 3] = 0x03;                                               // ETX
                FxCalculateCRC(PLCCommand).CopyTo(PLCCommand, PLCCommand.Length - 2); // CRC

            }
            return PLCCommand;


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


        internal OperationResult CheckReadResponse(byte[] ack)
        {
            if (ack.Length == 0) OperationResult.CreateFailedResult(StringResources.Language.MelsecFxReceiveZore);
            if (ack[0] == 0x15) OperationResult.CreateFailedResult(StringResources.Language.MelsecFxAckNagative + " Actual: " + ack.ToHexString());
            if (ack[0] != 0x02) OperationResult.CreateFailedResult(StringResources.Language.MelsecFxAckWrong + ack[0] + " Actual: " + ack.ToHexString());
            if (!checkCRC(ack)) OperationResult.CreateFailedResult(StringResources.Language.MelsecFxCrcCheckFailed);
            return OperationResult.CreateSuccessResult();

            bool checkCRC(byte[] data)
            {
                byte[] crc = FxCalculateCRC(data);
                if (crc[0] != data[data.Length - 2]) return false;
                if (crc[1] != data[data.Length - 1]) return false;
                return true;
            }

        }

        internal OperationResult CheckWriteResponse(byte[] ack)
        {
            if (ack.Length == 0) OperationResult.CreateFailedResult(StringResources.Language.MelsecFxReceiveZore);
            if (ack[0] == 0x15) OperationResult.CreateFailedResult(StringResources.Language.MelsecFxAckNagative + " Actual: " + ack.ToHexString());
            if (ack[0] != 0x06) OperationResult.CreateFailedResult(StringResources.Language.MelsecFxAckWrong + ack[0] + " Actual: " + ack.ToHexString());

            return OperationResult.CreateSuccessResult();
        }

    }
}
