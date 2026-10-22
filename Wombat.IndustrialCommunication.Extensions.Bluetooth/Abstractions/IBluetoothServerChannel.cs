namespace Wombat.IndustrialCommunication.Extensions.Bluetooth
{
    /// <summary>
    /// 蓝牙服务端（外设）通道：在 <see cref="IBluetoothChannel"/> 之上区分「在广播」与「已连接」。
    /// <para>
    /// 服务端角色下 <see cref="IBluetoothChannel.ConnectAsync"/> 的语义为「启动本机服务/广播并等待接受一个 central」；
    /// <see cref="IBluetoothChannel.Connected"/> = 存在活动字节链路（即至少有一个 central 已接入）；
    /// <see cref="IsAdvertising"/> = 正在广播。
    /// 断开时应如实上报，收发超时应返回结构化失败。
    /// </para>
    /// </summary>
    public interface IBluetoothServerChannel : IBluetoothChannel
    {
        /// <summary>
        /// 是否正在广播（与「已连接」相互独立：无 central 时可为 true）。
        /// </summary>
        bool IsAdvertising { get; }
    }
}
