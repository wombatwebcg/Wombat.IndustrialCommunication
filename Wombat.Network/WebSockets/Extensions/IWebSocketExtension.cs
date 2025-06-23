namespace Wombat.Network.WebSockets.Extensions
{
    /// <summary>
    /// 定义 WebSocket 扩展的契约，用于在 WebSocket 连接上提供额外功能。
    /// </summary>
    /// <remarks>
    /// WebSocket 扩展可以提供各种功能，如消息压缩、多路复用、自定义帧处理等。
    /// 扩展通过在握手过程中协商来启用，并可以修改消息的处理方式。
    /// 扩展还可以占用 WebSocket 帧头中的保留位（RSV1、RSV2、RSV3）来标识特殊处理。
    /// </remarks>
    public interface IWebSocketExtension
    {
        /// <summary>
        /// 获取扩展的名称。
        /// </summary>
        /// <value>扩展的唯一标识名称。</value>
        string Name { get; }

        /// <summary>
        /// 获取一个值，指示此扩展是否占用 WebSocket 帧头中的 RSV1 位。
        /// </summary>
        /// <value>如果占用 RSV1 位则为 <c>true</c>，否则为 <c>false</c>。</value>
        /// <remarks>
        /// RSV1、RSV2、RSV3 是 WebSocket 帧头中的保留位，扩展可以使用这些位来标识特殊处理。
        /// 多个扩展不能占用同一个保留位。
        /// </remarks>
        bool Rsv1BitOccupied { get; }

        /// <summary>
        /// 获取一个值，指示此扩展是否占用 WebSocket 帧头中的 RSV2 位。
        /// </summary>
        /// <value>如果占用 RSV2 位则为 <c>true</c>，否则为 <c>false</c>。</value>
        /// <remarks>
        /// RSV1、RSV2、RSV3 是 WebSocket 帧头中的保留位，扩展可以使用这些位来标识特殊处理。
        /// 多个扩展不能占用同一个保留位。
        /// </remarks>
        bool Rsv2BitOccupied { get; }

        /// <summary>
        /// 获取一个值，指示此扩展是否占用 WebSocket 帧头中的 RSV3 位。
        /// </summary>
        /// <value>如果占用 RSV3 位则为 <c>true</c>，否则为 <c>false</c>。</value>
        /// <remarks>
        /// RSV1、RSV2、RSV3 是 WebSocket 帧头中的保留位，扩展可以使用这些位来标识特殊处理。
        /// 多个扩展不能占用同一个保留位。
        /// </remarks>
        bool Rsv3BitOccupied { get; }

        /// <summary>
        /// 获取已协商的扩展提议字符串。
        /// </summary>
        /// <returns>表示已协商扩展配置的字符串。</returns>
        /// <remarks>
        /// 此方法返回在握手过程中协商确定的扩展配置，通常包含扩展名称和参数。
        /// 返回的字符串将用于 WebSocket 握手响应中的 Sec-WebSocket-Extensions 头部。
        /// </remarks>
        string GetAgreedOffer();

        /// <summary>
        /// 构建扩展数据，用于在 WebSocket 帧中传输扩展特定的信息。
        /// </summary>
        /// <param name="payload">原始负载数据。</param>
        /// <param name="offset">负载数据的起始偏移量。</param>
        /// <param name="count">要处理的字节数。</param>
        /// <returns>包含扩展数据的字节数组。</returns>
        /// <remarks>
        /// 此方法允许扩展在 WebSocket 帧的扩展数据字段中添加自定义信息。
        /// 扩展数据位于 WebSocket 帧的负载数据之前。
        /// </remarks>
        byte[] BuildExtensionData(byte[] payload, int offset, int count);

        /// <summary>
        /// 处理接收到的消息负载数据。
        /// </summary>
        /// <param name="payload">接收到的负载数据。</param>
        /// <param name="offset">负载数据的起始偏移量。</param>
        /// <param name="count">要处理的字节数。</param>
        /// <returns>处理后的负载数据。</returns>
        /// <remarks>
        /// 此方法在接收到 WebSocket 消息时被调用，允许扩展对消息进行解码、解压缩或其他处理。
        /// 处理顺序通常与扩展协商的顺序相反。
        /// </remarks>
        byte[] ProcessIncomingMessagePayload(byte[] payload, int offset, int count);

        /// <summary>
        /// 处理要发送的消息负载数据。
        /// </summary>
        /// <param name="payload">要发送的负载数据。</param>
        /// <param name="offset">负载数据的起始偏移量。</param>
        /// <param name="count">要处理的字节数。</param>
        /// <returns>处理后的负载数据。</returns>
        /// <remarks>
        /// 此方法在发送 WebSocket 消息时被调用，允许扩展对消息进行编码、压缩或其他处理。
        /// 处理顺序通常与扩展协商的顺序一致。
        /// </remarks>
        byte[] ProcessOutgoingMessagePayload(byte[] payload, int offset, int count);
    }
}
