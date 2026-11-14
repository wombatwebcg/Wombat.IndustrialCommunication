using System;

namespace Wombat.IndustrialCommunication.Iec104.Protocol
{
    /// <summary>
    /// 流式拆帧器。调用方持续 Append 收到的字节，循环 TryReadFrame 取整帧。
    /// 非 0x68 起始的字节会被逐个丢弃并计数，用于从丢包或脏数据中恢复。
    /// </summary>
    public sealed class Iec104FrameParser
    {
        private const int InitialCapacity = 1024;
        private const int MaxBufferSize = 64 * 1024;

        private byte[] _buffer = new byte[InitialCapacity];
        private int _count;

        /// <summary>因首字节不是 0x68 而丢弃的字节数。</summary>
        public int Resynchronizations { get; private set; }

        /// <summary>因长度非法而丢弃的帧数。</summary>
        public int InvalidFrames { get; private set; }

        /// <summary>当前缓冲中的字节数。</summary>
        public int BufferedBytes
        {
            get { return _count; }
        }

        /// <summary>追加接收到的字节。</summary>
        public void Append(byte[] data, int offset, int count)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || count < 0 || offset + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count == 0) return;
            if (count > MaxBufferSize)
                throw new Iec104ProtocolException(Iec104ErrorKind.CapacityExceeded, "单次追加的数据超过缓冲区上限。");

            EnsureCapacity(_count + count);
            Buffer.BlockCopy(data, offset, _buffer, _count, count);
            _count += count;
        }

        /// <summary>尝试取出一个完整帧；数据不足时返回 false。</summary>
        public bool TryReadFrame(out byte[] frame)
        {
            frame = null;

            while (true)
            {
                if (_count < 2) return false;

                if (_buffer[0] != Iec104Constants.StartByte)
                {
                    Drop(1);
                    Resynchronizations++;
                    continue;
                }

                var lengthField = _buffer[1];
                if (lengthField < 4 || lengthField + 2 > Iec104Constants.MaxApduLength)
                {
                    Drop(1);
                    InvalidFrames++;
                    continue;
                }

                var total = lengthField + 2;
                if (_count < total) return false;

                frame = new byte[total];
                Buffer.BlockCopy(_buffer, 0, frame, 0, total);
                Drop(total);
                return true;
            }
        }

        /// <summary>清空缓冲与统计，用于重连。</summary>
        public void Reset()
        {
            _count = 0;
            Resynchronizations = 0;
            InvalidFrames = 0;
        }

        private void Drop(int count)
        {
            if (count >= _count)
            {
                _count = 0;
                return;
            }

            Buffer.BlockCopy(_buffer, count, _buffer, 0, _count - count);
            _count -= count;
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _buffer.Length) return;

            var size = _buffer.Length;
            while (size < required && size < MaxBufferSize)
            {
                size *= 2;
            }

            if (size > MaxBufferSize) size = MaxBufferSize;
            if (size < required)
                throw new Iec104ProtocolException(Iec104ErrorKind.CapacityExceeded, "接收缓冲超过 64KB 上限，可能持续收到无起始字符的数据。");

            var next = new byte[size];
            Buffer.BlockCopy(_buffer, 0, next, 0, _count);
            _buffer = next;
        }
    }
}
