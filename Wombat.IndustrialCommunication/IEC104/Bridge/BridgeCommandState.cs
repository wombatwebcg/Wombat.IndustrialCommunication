namespace Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge
{
    /// <summary>写入命令寄存器的值。</summary>
    public static class BridgeCommandValue
    {
        public const ushort None = 0;
        public const ushort RequestOn = 1;
        public const ushort RequestOff = 2;
        public const ushort ConfirmOn = 3;
        public const ushort ConfirmOff = 4;
    }

    /// <summary>命令点状态寄存器的取值。</summary>
    public enum BridgeCommandState : ushort
    {
        Idle = 0,
        Selecting = 1,
        PendingOn = 2,
        PendingOff = 3,
        Executing = 4,
        Succeeded = 5,
        Rejected = 6,
        ConfirmTimeout = 7,
        Disabled = 8,
        Busy = 9,
    }

    /// <summary>命令点原因寄存器的取值。</summary>
    public enum BridgeCommandReason : ushort
    {
        None = 0,
        NegativeConfirm = 1,
        Timeout = 2,
        LinkUnavailable = 3,
        Disabled = 4,
        IoaNotAllowed = 5,
        ConfirmMismatch = 6,
        Busy = 7,
    }
}
