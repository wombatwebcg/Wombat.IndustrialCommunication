namespace Wombat.Network.WebSockets.Extensions
{
    /// <summary>
    /// 定义 WebSocket 扩展协商器的契约，用于在握手过程中协商扩展配置。
    /// </summary>
    /// <remarks>
    /// 扩展协商器负责处理 WebSocket 握手过程中的扩展协商逻辑，
    /// 包括解析客户端的扩展提议、验证参数有效性以及创建协商后的扩展实例。
    /// 协商器需要同时支持作为服务器端和客户端的协商行为。
    /// </remarks>
    public interface IWebSocketExtensionNegotiator
    {
        /// <summary>
        /// 作为服务器端协商 WebSocket 扩展。
        /// </summary>
        /// <param name="offer">客户端提供的扩展提议字符串。</param>
        /// <param name="invalidParameter">如果协商失败，输出无效的参数名称。</param>
        /// <param name="negotiatedExtension">如果协商成功，输出协商后的扩展实例。</param>
        /// <returns>如果协商成功则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        /// <remarks>
        /// 此方法在服务器端处理客户端的扩展协商请求时调用。
        /// 服务器需要解析客户端的提议，验证参数的有效性，并决定是否接受该扩展。
        /// 如果接受，则创建相应的扩展实例用于后续的消息处理。
        /// </remarks>
        bool NegotiateAsServer(string offer, out string invalidParameter, out IWebSocketExtension negotiatedExtension);

        /// <summary>
        /// 作为客户端协商 WebSocket 扩展。
        /// </summary>
        /// <param name="offer">服务器端返回的扩展协商响应字符串。</param>
        /// <param name="invalidParameter">如果协商失败，输出无效的参数名称。</param>
        /// <param name="negotiatedExtension">如果协商成功，输出协商后的扩展实例。</param>
        /// <returns>如果协商成功则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        /// <remarks>
        /// 此方法在客户端处理服务器的扩展协商响应时调用。
        /// 客户端需要验证服务器的响应是否符合预期，参数是否有效，
        /// 如果验证通过，则创建相应的扩展实例用于后续的消息处理。
        /// </remarks>
        bool NegotiateAsClient(string offer, out string invalidParameter, out IWebSocketExtension negotiatedExtension);
    }
}
