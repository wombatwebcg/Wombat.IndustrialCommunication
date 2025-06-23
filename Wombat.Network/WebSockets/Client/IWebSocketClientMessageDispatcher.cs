using System.Threading.Tasks;

namespace Wombat.Network.WebSockets
{
    /// <summary>
    /// 定义 WebSocket 客户端消息分发器的契约，用于处理从服务器接收到的各种消息和事件。
    /// </summary>
    /// <remarks>
    /// 消息分发器是 WebSocket 客户端的核心组件，负责将接收到的消息和连接事件
    /// 分发到相应的处理方法。它支持文本消息、二进制消息、连接状态变化以及消息分片处理。
    /// 所有方法都是异步的，以支持高并发的消息处理。
    /// </remarks>
    public interface IWebSocketClientMessageDispatcher
    {
        /// <summary>
        /// 当客户端成功连接到服务器时调用。
        /// </summary>
        /// <param name="client">已连接的 WebSocket 客户端实例。</param>
        /// <returns>表示异步操作的任务。</returns>
        /// <remarks>
        /// 此方法在 WebSocket 握手成功完成后被调用，
        /// 可以在此方法中执行连接建立后的初始化操作。
        /// </remarks>
        Task OnServerConnected(WebSocketClient client);

        /// <summary>
        /// 当从服务器接收到文本消息时调用。
        /// </summary>
        /// <param name="client">接收消息的 WebSocket 客户端实例。</param>
        /// <param name="text">接收到的文本消息内容。</param>
        /// <returns>表示异步操作的任务。</returns>
        /// <remarks>
        /// 此方法处理 WebSocket 的文本帧（OpCode.Text），
        /// 消息内容已经解码为 UTF-8 字符串。
        /// </remarks>
        Task OnServerTextReceived(WebSocketClient client, string text);

        /// <summary>
        /// 当从服务器接收到二进制消息时调用。
        /// </summary>
        /// <param name="client">接收消息的 WebSocket 客户端实例。</param>
        /// <param name="data">包含二进制数据的字节数组。</param>
        /// <param name="offset">数据在数组中的起始偏移量。</param>
        /// <param name="count">二进制数据的字节数。</param>
        /// <returns>表示异步操作的任务。</returns>
        /// <remarks>
        /// 此方法处理 WebSocket 的二进制帧（OpCode.Binary），
        /// 数据以原始字节形式提供，需要根据应用协议进行解析。
        /// </remarks>
        Task OnServerBinaryReceived(WebSocketClient client, byte[] data, int offset, int count);

        /// <summary>
        /// 当客户端与服务器的连接断开时调用。
        /// </summary>
        /// <param name="client">已断开连接的 WebSocket 客户端实例。</param>
        /// <returns>表示异步操作的任务。</returns>
        /// <remarks>
        /// 此方法在连接断开时被调用，无论是正常关闭还是异常断开。
        /// 可以在此方法中执行清理操作或重连逻辑。
        /// </remarks>
        Task OnServerDisconnected(WebSocketClient client);

        /// <summary>
        /// 当服务器开始发送分片消息流时调用。
        /// </summary>
        /// <param name="client">接收消息的 WebSocket 客户端实例。</param>
        /// <param name="data">分片消息的首个片段数据。</param>
        /// <param name="offset">数据在数组中的起始偏移量。</param>
        /// <param name="count">此片段的字节数。</param>
        /// <returns>表示异步操作的任务。</returns>
        /// <remarks>
        /// 此方法处理 WebSocket 分片消息的第一个片段。
        /// 大消息可能被分割成多个片段进行传输，以避免阻塞其他消息。
        /// </remarks>
        Task OnServerFragmentationStreamOpened(WebSocketClient client, byte[] data, int offset, int count);

        /// <summary>
        /// 当服务器继续发送分片消息流的中间片段时调用。
        /// </summary>
        /// <param name="client">接收消息的 WebSocket 客户端实例。</param>
        /// <param name="data">分片消息的中间片段数据。</param>
        /// <param name="offset">数据在数组中的起始偏移量。</param>
        /// <param name="count">此片段的字节数。</param>
        /// <returns>表示异步操作的任务。</returns>
        /// <remarks>
        /// 此方法处理 WebSocket 分片消息的中间片段（Continuation 帧）。
        /// 可能会被多次调用，直到接收到最后一个片段。
        /// </remarks>
        Task OnServerFragmentationStreamContinued(WebSocketClient client, byte[] data, int offset, int count);

        /// <summary>
        /// 当服务器发送分片消息流的最后一个片段时调用。
        /// </summary>
        /// <param name="client">接收消息的 WebSocket 客户端实例。</param>
        /// <param name="data">分片消息的最后片段数据。</param>
        /// <param name="offset">数据在数组中的起始偏移量。</param>
        /// <param name="count">此片段的字节数。</param>
        /// <returns>表示异步操作的任务。</returns>
        /// <remarks>
        /// 此方法处理 WebSocket 分片消息的最后一个片段，标志着完整消息的结束。
        /// 在此方法中可以对完整的分片消息进行最终处理。
        /// </remarks>
        Task OnServerFragmentationStreamClosed(WebSocketClient client, byte[] data, int offset, int count);
    }
}
