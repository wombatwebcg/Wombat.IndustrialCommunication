using System;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 三菱 MC 协议地址：解析软元件前缀、校验编号进制与范围，并按 1E 规则展开别名。
    /// </summary>
    public sealed class McAddress
    {
        private McAddress(string originalAddress, string prefix, McDeviceDescriptor device, int address, bool isAliased)
        {
            OriginalAddress = originalAddress;
            Prefix = prefix;
            Device = device;
            Address = address;
            IsAliased = isAliased;
        }

        /// <summary>原始地址字符串</summary>
        public string OriginalAddress { get; private set; }

        /// <summary>用户书写的软元件前缀（别名展开前，大写）</summary>
        public string Prefix { get; private set; }

        /// <summary>解析后的软元件描述符（别名已展开）</summary>
        public McDeviceDescriptor Device { get; private set; }

        /// <summary>归一化软元件种类（别名已展开）</summary>
        public McDeviceKind DeviceKind
        {
            get { return Device.Kind; }
        }

        /// <summary>起始编号（十进制）</summary>
        public int Address { get; private set; }

        /// <summary>是否位软元件</summary>
        public bool IsBitDevice
        {
            get { return Device.IsBit; }
        }

        /// <summary>每个元素占用的字数（iQ-R 长定时器为 4）</summary>
        public int WordsPerPoint
        {
            get { return Device.WordsPerPoint; }
        }

        /// <summary>是否发生了 1E 别名展开（如 SM1200 → M9200）</summary>
        public bool IsAliased { get; private set; }

        /// <summary>
        /// 解析地址，非法或不支持时抛出异常：
        /// <see cref="ArgumentException"/> 表示地址格式/进制/范围错误，
        /// <see cref="NotSupportedException"/> 表示当前「帧 × 系列」不支持该软元件。
        /// </summary>
        public static McAddress Parse(string address, McProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("三菱 MC 地址不能为空", nameof(address));

            string text = address.Trim().ToUpperInvariant();

            McDeviceDescriptor device;
            int prefixLength;
            if (!McDeviceDescriptor.TryMatchPrefix(text, out device, out prefixLength))
                throw new ArgumentException($"无法识别的三菱软元件前缀: {address}", nameof(address));

            string prefix = text.Substring(0, prefixLength);
            string numberText = text.Substring(prefixLength);
            if (numberText.Length == 0)
                throw new ArgumentException($"软元件 {prefix} 缺少编号: {address}", nameof(address));

            int radix = profile.GetRadix(device);
            int number = ParseNumber(numberText, radix, address);

            profile.EnsureSupported(device);

            long maxNumber = (1L << (8 * profile.AddressWidth)) - 1;
            if (number > maxNumber)
                throw new ArgumentException($"软元件编号超出 {profile.AddressWidth} 字节范围: {address}", nameof(address));

            bool isAliased = false;
            if (profile.FrameType == McFrameType.A1E && device.AliasOf != McDeviceKind.None)
            {
                number += device.AliasOffset;
                device = McDeviceDescriptor.Find(device.AliasOf);
                isAliased = true;
            }

            return new McAddress(address, prefix, device, number, isAliased);
        }

        /// <summary>尝试解析地址，失败时返回 false 并给出原因</summary>
        public static bool TryParse(string address, McProfile profile, out McAddress result, out string error)
        {
            try
            {
                result = Parse(address, profile);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is OverflowException)
            {
                result = null;
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 把「元素个数」换算为协议点数。字软元件按 <see cref="WordsPerPoint"/> 放大
        /// （如 iQ-R 的 LTN 2 个元素 = 8 点），位软元件 1 元素 = 1 点。
        /// 访问单位与软元件类型不一致时本地报错。
        /// </summary>
        /// <param name="elementCount">元素个数（1 个 Int32 就是 1）</param>
        /// <param name="isBit">是否位访问</param>
        public int GetPointCount(int elementCount, bool isBit)
        {
            if (elementCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount, "元素个数必须大于 0");

            if (isBit && !Device.IsBit)
                throw new ArgumentException($"软元件 {Device.Prefix} 为字软元件，不支持位访问", nameof(isBit));

            if (!isBit && Device.IsBit)
                throw new ArgumentException($"软元件 {Device.Prefix} 为位软元件，不支持字访问", nameof(isBit));

            return isBit ? elementCount : elementCount * Device.WordsPerPoint;
        }

        /// <summary>
        /// 返回从当前设备号偏移若干元素的副本，用于分块与批量合并。
        /// 偏移只改变协议设备号，不改变软元件种类与别名状态。
        /// </summary>
        public McAddress WithOffset(int elementOffset)
        {
            return new McAddress(OriginalAddress, Prefix, Device, Address + elementOffset, IsAliased);
        }

        /// <summary>取协议设备号字节（小端）</summary>
        public byte[] GetDeviceNumberBytes(int width)
        {
            var buffer = new byte[width];
            int value = Address;
            for (int i = 0; i < width; i++)
            {
                buffer[i] = (byte)(value & 0xFF);
                value >>= 8;
            }

            return buffer;
        }

        /// <summary>按当前软元件与编号重建可读地址</summary>
        public override string ToString()
        {
            return OriginalAddress;
        }

        private static int ParseNumber(string numberText, int radix, string original)
        {
            long value = 0;
            for (int i = 0; i < numberText.Length; i++)
            {
                int digit = DigitValue(numberText[i]);
                if (digit < 0 || digit >= radix)
                    throw new ArgumentException($"地址编号 \"{numberText}\" 不是合法的 {radix} 进制数: {original}", nameof(original));

                value = value * radix + digit;
                if (value > int.MaxValue)
                    throw new ArgumentException($"地址编号过大: {original}", nameof(original));
            }

            return (int)value;
        }

        private static int DigitValue(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            return -1;
        }
    }
}
