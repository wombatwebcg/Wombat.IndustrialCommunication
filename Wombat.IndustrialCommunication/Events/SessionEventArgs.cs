using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 会话事件参数
    /// </summary>
    public class SessionEventArgs : EventArgs
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="session">会话</param>
        public SessionEventArgs(INetworkSession session)
        {
            Session = session;
        }

        /// <summary>
        /// 会话
        /// </summary>
        public INetworkSession Session { get; }
    }
} 