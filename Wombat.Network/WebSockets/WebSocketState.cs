using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.Network.WebSockets
{
    /// <summary>
    /// 表示WebSocket连接的状态。
    /// </summary>
    public enum WebSocketState
    {
        /// <summary>
        /// 初始状态，表示WebSocket连接尚未建立。
        /// </summary>
        None = 0,
        
        /// <summary>
        /// 正在连接状态，表示正在尝试建立WebSocket连接。
        /// </summary>
        Connecting = 1,
        
        /// <summary>
        /// 已打开状态，表示WebSocket连接已成功建立并可以进行数据传输。
        /// </summary>
        Open = 2,
        
        /// <summary>
        /// 正在关闭状态，表示正在关闭WebSocket连接。
        /// </summary>
        Closing = 3,
        
        /// <summary>
        /// 已关闭状态，表示WebSocket连接已关闭或断开。
        /// </summary>
        Closed = 5,
    }
}
