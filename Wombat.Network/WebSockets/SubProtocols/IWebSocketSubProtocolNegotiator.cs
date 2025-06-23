namespace Wombat.Network.WebSockets.SubProtocols
{
    /// <summary>
    /// 定义 WebSocket 子协议协商器的契约，用于在握手过程中协商子协议配置。
    /// </summary>
    /// <remarks>
    /// 子协议协商器负责处理 WebSocket 握手过程中的子协议协商逻辑。
    /// WebSocket 子协议定义了在 WebSocket 连接上使用的应用层协议，
    /// 允许客户端和服务器就特定的通信协议达成一致。
    /// 协商器需要同时支持作为服务器端和客户端的协商行为。
    /// </remarks>
    public interface IWebSocketSubProtocolNegotiator
    {
        /// <summary>
        /// 作为客户端协商 WebSocket 子协议。
        /// </summary>
        /// <param name="protocolName">子协议的名称。</param>
        /// <param name="protocolVersion">子协议的版本。</param>
        /// <param name="protocolParameter">子协议的参数字符串。</param>
        /// <param name="invalidParameter">如果协商失败，输出无效的参数名称。</param>
        /// <param name="negotiatedSubProtocol">如果协商成功，输出协商后的子协议实例。</param>
        /// <returns>如果协商成功则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        /// <remarks>
        /// 此方法在客户端处理服务器的子协议协商响应时调用。
        /// 客户端需要验证服务器选择的子协议是否在客户端支持的协议列表中，
        /// 并验证协议版本和参数的有效性。
        /// </remarks>
        bool NegotiateAsClient(string protocolName, string protocolVersion, string protocolParameter, out string invalidParameter, out IWebSocketSubProtocol negotiatedSubProtocol);

        /// <summary>
        /// 作为服务器端协商 WebSocket 子协议。
        /// </summary>
        /// <param name="protocolName">客户端请求的子协议名称。</param>
        /// <param name="protocolVersion">客户端请求的子协议版本。</param>
        /// <param name="protocolParameter">客户端提供的子协议参数字符串。</param>
        /// <param name="invalidParameter">如果协商失败，输出无效的参数名称。</param>
        /// <param name="negotiatedSubProtocol">如果协商成功，输出协商后的子协议实例。</param>
        /// <returns>如果协商成功则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        /// <remarks>
        /// 此方法在服务器端处理客户端的子协议协商请求时调用。
        /// 服务器需要从客户端提供的子协议列表中选择一个支持的协议，
        /// 验证协议版本和参数的兼容性，并创建相应的子协议实例。
        /// </remarks>
        bool NegotiateAsServer(string protocolName, string protocolVersion, string protocolParameter, out string invalidParameter, out IWebSocketSubProtocol negotiatedSubProtocol);
    }
}
