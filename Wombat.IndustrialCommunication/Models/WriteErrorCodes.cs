namespace Wombat.IndustrialCommunication
{
    public static class WriteErrorCodes
    {
        public const int Success = 0;

        public const int InvalidParameter = 1001;
        public const int InvalidAddress = 1002;
        public const int DataTypeMismatch = 1003;
        public const int InvalidValue = 1004;

        public const int ConnectionNotEstablished = 2001;
        public const int ChannelUnavailable = 2002;
        public const int OperationTimeout = 2003;
        public const int OperationCancelled = 2004;

        public const int DeviceBusy = 3001;
        public const int DeviceRejectedWrite = 3002;
        public const int ProtocolException = 3003;
        public const int BatchPartialFailure = 3004;

        public static bool IsValidation(int errorCode)
        {
            return errorCode >= 1001 && errorCode <= 1004;
        }

        public static bool IsChannel(int errorCode)
        {
            return errorCode >= 2001 && errorCode <= 2004;
        }

        public static bool IsRetryable(int errorCode)
        {
            return errorCode == ConnectionNotEstablished
                || errorCode == ChannelUnavailable
                || errorCode == OperationTimeout
                || errorCode == DeviceBusy
                || errorCode == BatchPartialFailure;
        }

        public static int NormalizeModbusWriteError(int modbusExceptionCode)
        {
            switch (modbusExceptionCode)
            {
                case 0x01:
                    return ProtocolException;
                case 0x02:
                    return InvalidAddress;
                case 0x03:
                    return InvalidValue;
                case 0x04:
                    return DeviceRejectedWrite;
                case 0x05:
                    return OperationTimeout;
                case 0x06:
                    return DeviceBusy;
                default:
                    return ProtocolException;
            }
        }

        public static int NormalizeFinsWriteError(ushort finsErrorCode)
        {
            if (finsErrorCode == 0)
            {
                return Success;
            }

            var mres = (byte)(finsErrorCode >> 8);
            var sres = (byte)(finsErrorCode & 0xFF);

            if (mres == 0x01 && sres == 0x00)
            {
                return OperationTimeout;
            }

            if (mres == 0x01 && sres == 0x01)
            {
                return OperationCancelled;
            }

            if (mres == 0x01 || mres == 0x02 || mres == 0x03)
            {
                return ConnectionNotEstablished;
            }

            if ((mres == 0x11 && sres >= 0x01 && sres <= 0x03)
                || (mres == 0x20 && sres >= 0x01 && sres <= 0x0F))
            {
                return InvalidAddress;
            }

            if ((mres == 0x11 && sres == 0x04)
                || (mres == 0x20 && sres >= 0x10 && sres <= 0x1F))
            {
                return InvalidValue;
            }

            if (mres == 0x22 || mres == 0x23)
            {
                return DeviceRejectedWrite;
            }

            return ProtocolException;
        }
    }
}
