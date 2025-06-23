using System;
using System.Text;

namespace Wombat.Network
{
    /// <summary>
    /// 表示行分隔符，用于基于行的帧分隔。
    /// </summary>
    public class LineDelimiter : IEquatable<LineDelimiter>
    {
        /// <summary>
        /// CRLF行分隔符（\r\n），通常用于Windows系统。
        /// </summary>
        public static readonly LineDelimiter CRLF = new LineDelimiter("\r\n");
        
        /// <summary>
        /// Unix行分隔符（\n），通常用于Unix/Linux系统。
        /// </summary>
        public static readonly LineDelimiter UNIX = new LineDelimiter("\n");
        
        /// <summary>
        /// Mac行分隔符（\r），通常用于旧版Mac系统。
        /// </summary>
        public static readonly LineDelimiter MAC = new LineDelimiter("\r");
        
        /// <summary>
        /// Windows行分隔符，等同于CRLF。
        /// </summary>
        public static readonly LineDelimiter WINDOWS = CRLF;

        /// <summary>
        /// 初始化 <see cref="LineDelimiter"/> 类的新实例。
        /// </summary>
        /// <param name="delimiter">分隔符字符串。</param>
        public LineDelimiter(string delimiter)
        {
            this.DelimiterString = delimiter;
            this.DelimiterChars = this.DelimiterString.ToCharArray();
            this.DelimiterBytes = Encoding.UTF8.GetBytes(this.DelimiterChars);
        }

        /// <summary>
        /// 获取分隔符字符串。
        /// </summary>
        public string DelimiterString { get; private set; }
        
        /// <summary>
        /// 获取分隔符字符数组。
        /// </summary>
        public char[] DelimiterChars { get; private set; }
        
        /// <summary>
        /// 获取分隔符字节数组。
        /// </summary>
        public byte[] DelimiterBytes { get; private set; }

        /// <summary>
        /// 确定当前行分隔符是否与指定的行分隔符相等。
        /// </summary>
        /// <param name="other">要比较的行分隔符。</param>
        /// <returns>如果相等则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool Equals(LineDelimiter other)
        {
            if (Object.ReferenceEquals(other, null)) return false;
            if (Object.ReferenceEquals(this, other)) return true;

            return (StringComparer.OrdinalIgnoreCase.Compare(this.DelimiterString, other.DelimiterString) == 0);
        }

        /// <summary>
        /// 确定当前行分隔符是否与指定的对象相等。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>如果相等则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as LineDelimiter);
        }

        /// <summary>
        /// 获取当前行分隔符的哈希代码。
        /// </summary>
        /// <returns>哈希代码。</returns>
        public override int GetHashCode()
        {
            return this.DelimiterString.GetHashCode();
        }

        /// <summary>
        /// 返回当前行分隔符的字符串表示形式。
        /// </summary>
        /// <returns>分隔符字符串。</returns>
        public override string ToString()
        {
            return this.DelimiterString;
        }
    }

    /// <summary>
    /// 基于行分隔符的帧构建器，使用指定的行分隔符来分隔数据帧。
    /// </summary>
    public sealed class LineBasedFrameBuilder : FrameBuilder
    {
        /// <summary>
        /// 初始化 <see cref="LineBasedFrameBuilder"/> 类的新实例，使用默认的CRLF分隔符。
        /// </summary>
        public LineBasedFrameBuilder()
            : this(new LineBasedFrameEncoder(), new LineBasedFrameDecoder())
        {
        }

        /// <summary>
        /// 初始化 <see cref="LineBasedFrameBuilder"/> 类的新实例。
        /// </summary>
        /// <param name="delimiter">用于分隔帧的行分隔符。</param>
        public LineBasedFrameBuilder(LineDelimiter delimiter)
            : this(new LineBasedFrameEncoder(delimiter), new LineBasedFrameDecoder(delimiter))
        {
        }

        /// <summary>
        /// 初始化 <see cref="LineBasedFrameBuilder"/> 类的新实例。
        /// </summary>
        /// <param name="encoder">用于编码帧的编码器。</param>
        /// <param name="decoder">用于解码帧的解码器。</param>
        public LineBasedFrameBuilder(LineBasedFrameEncoder encoder, LineBasedFrameDecoder decoder)
            : base(encoder, decoder)
        {
        }
    }

    /// <summary>
    /// 基于行分隔符的帧编码器，在数据后添加指定的行分隔符。
    /// </summary>
    public sealed class LineBasedFrameEncoder : IFrameEncoder
    {
        private readonly LineDelimiter _delimiter;

        /// <summary>
        /// 初始化 <see cref="LineBasedFrameEncoder"/> 类的新实例，使用默认的CRLF分隔符。
        /// </summary>
        public LineBasedFrameEncoder()
            : this(LineDelimiter.CRLF)
        {
        }

        /// <summary>
        /// 初始化 <see cref="LineBasedFrameEncoder"/> 类的新实例。
        /// </summary>
        /// <param name="delimiter">用于分隔帧的行分隔符。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="delimiter"/> 为 <c>null</c> 时引发。</exception>
        public LineBasedFrameEncoder(LineDelimiter delimiter)
        {
            if (delimiter == null)
                throw new ArgumentNullException("delimiter");
            _delimiter = delimiter;
        }

        /// <summary>
        /// 获取行分隔符。
        /// </summary>
        public LineDelimiter LineDelimiter { get { return _delimiter; } }

        /// <summary>
        /// 将指定的数据编码为带行分隔符的帧格式。
        /// </summary>
        /// <param name="payload">要编码的原始数据。</param>
        /// <param name="offset">数据的起始偏移量。</param>
        /// <param name="count">要编码的数据长度。</param>
        /// <param name="frameBuffer">输出参数，编码后的帧数据缓冲区。</param>
        /// <param name="frameBufferOffset">输出参数，帧数据在缓冲区中的起始偏移量。</param>
        /// <param name="frameBufferLength">输出参数，帧数据的总长度。</param>
        public void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
        {
            var buffer = new byte[count + _delimiter.DelimiterBytes.Length];
            Array.Copy(payload, offset, buffer, 0, count);
            Array.Copy(_delimiter.DelimiterBytes, 0, buffer, count, _delimiter.DelimiterBytes.Length);

            frameBuffer = buffer;
            frameBufferOffset = 0;
            frameBufferLength = buffer.Length;
        }
    }

    /// <summary>
    /// 基于行分隔符的帧解码器，通过查找行分隔符来解码数据帧。
    /// </summary>
    public sealed class LineBasedFrameDecoder : IFrameDecoder
    {
        private readonly LineDelimiter _delimiter;

        /// <summary>
        /// 初始化 <see cref="LineBasedFrameDecoder"/> 类的新实例，使用默认的CRLF分隔符。
        /// </summary>
        public LineBasedFrameDecoder()
            : this(LineDelimiter.CRLF)
        {
        }

        /// <summary>
        /// 初始化 <see cref="LineBasedFrameDecoder"/> 类的新实例。
        /// </summary>
        /// <param name="delimiter">用于分隔帧的行分隔符。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="delimiter"/> 为 <c>null</c> 时引发。</exception>
        public LineBasedFrameDecoder(LineDelimiter delimiter)
        {
            if (delimiter == null)
                throw new ArgumentNullException("delimiter");
            _delimiter = delimiter;
        }

        /// <summary>
        /// 获取行分隔符。
        /// </summary>
        public LineDelimiter LineDelimiter { get { return _delimiter; } }

        /// <summary>
        /// 尝试从指定的缓冲区中解码出一个完整的基于行分隔符的帧。
        /// </summary>
        /// <param name="buffer">包含待解码数据的缓冲区。</param>
        /// <param name="offset">缓冲区中数据的起始偏移量。</param>
        /// <param name="count">缓冲区中可用数据的长度。</param>
        /// <param name="frameLength">输出参数，解码的帧的总长度（包括分隔符）。</param>
        /// <param name="payload">输出参数，解码后的原始数据（不包括分隔符）。</param>
        /// <param name="payloadOffset">输出参数，原始数据在输出缓冲区中的起始偏移量。</param>
        /// <param name="payloadCount">输出参数，原始数据的长度。</param>
        /// <returns>如果成功找到分隔符并解码出完整帧则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            if (count < _delimiter.DelimiterBytes.Length)
                return false;

            var delimiter = _delimiter.DelimiterBytes;
            bool matched = false;
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < delimiter.Length; j++)
                {
                    if (i + j < count && buffer[offset + i + j] == delimiter[j])
                    {
                        matched = true;
                    }
                    else
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    frameLength = i + delimiter.Length;
                    payload = buffer;
                    payloadOffset = offset;
                    payloadCount = i;
                    return true;
                }
            }

            return false;
        }
    }
}
