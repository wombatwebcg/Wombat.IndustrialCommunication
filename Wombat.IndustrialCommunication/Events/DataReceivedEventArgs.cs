using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 数据接收事件参数
    /// </summary>
    public class DataReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="session">会话</param>
        /// <param name="data">数据</param>
        public DataReceivedEventArgs(INetworkSession session, byte[] data)
        {
            Session = session;
            Data = data;
            Offset = 0;
            Count = data.Length;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="session">会话</param>
        /// <param name="data">数据</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">数量</param>
        public DataReceivedEventArgs(INetworkSession session, byte[] data, int offset, int count)
        {
            Session = session;
            Data = data;
            Offset = offset;
            Count = count;
        }

        /// <summary>
        /// 会话
        /// </summary>
        public INetworkSession Session { get; }

        /// <summary>
        /// 数据
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// 偏移量
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// 数量
        /// </summary>
        public int Count { get; }
    }
} 