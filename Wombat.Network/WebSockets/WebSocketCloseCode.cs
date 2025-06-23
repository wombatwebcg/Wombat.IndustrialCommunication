namespace Wombat.Network.WebSockets
{
    /// <summary>
    /// 表示WebSocket连接关闭的状态代码。
    /// </summary>
    /// <remarks>
    /// 状态代码范围说明：
    /// <list type="bullet">
    /// <item><description>0-999: 未使用的状态代码范围</description></item>
    /// <item><description>1000-1999: 协议保留的状态代码范围</description></item>
    /// <item><description>2000-2999: 扩展保留的状态代码范围</description></item>
    /// <item><description>3000-3999: 库和框架可使用的状态代码范围</description></item>
    /// <item><description>4000-4999: 应用程序代码可使用的状态代码范围</description></item>
    /// </list>
    /// </remarks>
    public enum WebSocketCloseCode
    {
        /// <summary>
        /// 正常关闭，表示连接的目的已经完成。
        /// </summary>
        NormalClosure = 1000,
        
        /// <summary>
        /// 端点不可用，表示服务器正在关闭或客户端离开了页面。
        /// </summary>
        EndpointUnavailable = 1001,
        
        /// <summary>
        /// 协议错误，表示由于协议错误而终止连接。
        /// </summary>
        ProtocolError = 1002,
        
        /// <summary>
        /// 无效的消息类型，表示收到了不能处理的数据类型。
        /// </summary>
        InvalidMessageType = 1003,
        
        /// <summary>
        /// 空关闭代码，保留值，不应在关闭帧中使用。
        /// </summary>
        Empty = 1005,
        
        /// <summary>
        /// 异常关闭，保留值，表示连接异常断开，不应由用户代码使用。
        /// </summary>
        AbnormalClosure = 1006,
        
        /// <summary>
        /// 无效的载荷数据，表示收到了不符合消息类型的数据。
        /// </summary>
        InvalidPayloadData = 1007,
        
        /// <summary>
        /// 策略违反，表示收到了违反策略的消息。
        /// </summary>
        PolicyViolation = 1008,
        
        /// <summary>
        /// 消息过大，表示收到的消息对于处理来说太大。
        /// </summary>
        MessageTooBig = 1009,
        
        /// <summary>
        /// 缺少强制扩展，表示客户端期望服务器协商一个或多个扩展，但服务器没有协商。
        /// </summary>
        MandatoryExtension = 1010,
        
        /// <summary>
        /// 内部服务器错误，表示服务器遇到了意外情况，阻止其完成请求。
        /// </summary>
        InternalServerError = 1011,
        
        /// <summary>
        /// TLS握手失败，保留值，表示TLS握手失败，不应由用户代码使用。
        /// </summary>
        TlsHandshakeFailed = 1015,
    }
}
