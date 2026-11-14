using System;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Diagnostics
{
    /// <summary>链路状态。</summary>
    public enum Iec104LinkState
    {
        Connecting,
        Connected,
        DataTransferActive,
        Disconnected,
        Failed,
    }

    /// <summary>链路状态变化参数。</summary>
    public sealed class Iec104LinkEventArgs : EventArgs
    {
        public Iec104LinkEventArgs(Iec104LinkState state, string message, Exception exception = null)
        {
            State = state;
            Message = message;
            Exception = exception;
        }

        public Iec104LinkState State { get; }

        public string Message { get; }

        public Exception Exception { get; }
    }

    /// <summary>收发帧追踪参数。</summary>
    public sealed class Iec104TraceEventArgs : EventArgs
    {
        public Iec104TraceEventArgs(bool isSending, byte[] data, string meaning)
        {
            IsSending = isSending;
            Data = data;
            Meaning = meaning;
            Timestamp = DateTime.Now;
        }

        public bool IsSending { get; }

        public byte[] Data { get; }

        public string Meaning { get; }

        public DateTime Timestamp { get; }

        public string Hex
        {
            get
            {
                if (Data == null) return string.Empty;
                var text = new string[Data.Length];
                for (var i = 0; i < Data.Length; i++) text[i] = Data[i].ToString("X2");
                return string.Join(" ", text);
            }
        }
    }

    /// <summary>收到值变化参数。</summary>
    public sealed class Iec104ValueEventArgs : EventArgs
    {
        public Iec104ValueEventArgs(Model.Iec104Value value)
        {
            Value = value;
        }

        public Model.Iec104Value Value { get; }
    }

    /// <summary>从站收到遥控请求。</summary>
    public sealed class Iec104CommandRequestEventArgs : EventArgs
    {
        public Iec104CommandRequestEventArgs(
            int ioa,
            bool isSelect,
            bool? singlePointValue,
            Protocol.Iec104DoublePointState? doublePointState)
        {
            Ioa = ioa;
            IsSelect = isSelect;
            SinglePointValue = singlePointValue;
            DoublePointState = doublePointState;
        }

        public int Ioa { get; }

        /// <summary>true 为选择，false 为执行。</summary>
        public bool IsSelect { get; }

        public bool? SinglePointValue { get; }

        public Protocol.Iec104DoublePointState? DoublePointState { get; }

        /// <summary>置 true 表示拒绝本次遥控。</summary>
        public bool Cancel { get; set; }

        /// <summary>拒绝原因，写入日志用。</summary>
        public string RejectReason { get; set; }
    }

    /// <summary>客户端连接/断开参数。</summary>
    public sealed class Iec104ClientEventArgs : EventArgs
    {
        public Iec104ClientEventArgs(Guid sessionId, System.Net.EndPoint remoteEndPoint)
        {
            SessionId = sessionId;
            RemoteEndPoint = remoteEndPoint;
        }

        public Guid SessionId { get; }

        public System.Net.EndPoint RemoteEndPoint { get; }
    }
}
