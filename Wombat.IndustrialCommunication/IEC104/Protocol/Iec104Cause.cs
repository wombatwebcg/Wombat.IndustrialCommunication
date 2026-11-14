namespace Wombat.IndustrialCommunication.Extensions.IEC104.Protocol
{
    /// <summary>
    /// 传送原因。低六位有效，P/N 与 T 位单独存放。
    /// </summary>
    public enum Iec104Cause : byte
    {
        NotUsed = 0,
        Periodic = 1,
        Background = 2,
        Spontaneous = 3,
        Initialized = 4,
        Request = 5,
        Activation = 6,
        ActivationConfirmation = 7,
        Deactivation = 8,
        DeactivationConfirmation = 9,
        ActivationTermination = 10,
        ReturnInformationRemote = 11,
        ReturnInformationLocal = 12,
        FileTransfer = 13,
        InterrogatedByStation = 20,
        InterrogatedByGroup1 = 21,
        InterrogatedByGroup16 = 36,
        UnknownTypeId = 44,
        UnknownCause = 45,
        UnknownCommonAddress = 46,
        UnknownInformationObjectAddress = 47,
    }
}
