
using System;

namespace Wombat.Network
{
    /// <summary>
    /// 协议类
    /// </summary>
    public struct NetWorkProtocol
    {
        /// <summary>
        /// 值
        /// </summary>
        private readonly string _value;

        /// <summary>
        /// 表示无协议
        /// </summary>
        public static readonly NetWorkProtocol None = new NetWorkProtocol();

        /// <summary>
        /// 获取http协议
        /// </summary>
        public static readonly NetWorkProtocol Http = new NetWorkProtocol("http");

        /// <summary>
        /// TCP协议
        /// </summary>
        public static readonly NetWorkProtocol TCP = new NetWorkProtocol("tcp");

        /// <summary>
        /// UDP协议
        /// </summary>
        public static readonly NetWorkProtocol UDP = new NetWorkProtocol("udp");

        /// <summary>
        /// 获取WebSocket协议
        /// </summary>
        public static readonly NetWorkProtocol WebSocket = new NetWorkProtocol("ws");

        /// <summary>
        /// 表示
        /// </summary>
        /// <param name="value">值</param>
        public NetWorkProtocol(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException();
            }
            this._value = value;
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(_value))
            {
                return "None";
            }
            return _value;
        }

        /// <summary>
        /// 获取哈希码
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            if (_value == null)
            {
                return string.Empty.GetHashCode();
            }
            return _value.ToLower().GetHashCode();
        }

        /// <summary>
        /// 比较是否和目标相等
        /// </summary>
        /// <param name="obj">目标</param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is NetWorkProtocol)
            {
                return GetHashCode() == obj.GetHashCode();
            }
            return false;
        }

        /// <summary>
        /// 等于
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator ==(NetWorkProtocol a, NetWorkProtocol b)
        {
            if (string.IsNullOrEmpty(a._value) && string.IsNullOrEmpty(b._value))
            {
                return true;
            }
            return string.Equals(a._value, b._value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 不等于
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator !=(NetWorkProtocol a, NetWorkProtocol b)
        {
            var state = a == b;
            return !state;
        }
    }
}