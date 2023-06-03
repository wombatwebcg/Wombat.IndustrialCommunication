using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication
{
    public interface IDataTofer
    {
        /// <summary>
        /// 读取的数据长度，对于西门子，等同于字节数，对于三菱和Modbus为字节数的一半
        /// </summary>
        ushort ReadCount { get; }

        /// <summary>
        /// 从字节数组进行解析实际的对象
        /// </summary>
        /// <param name="Value">从远程读取的数据源</param>
        void ParseSource(byte[] Value);

        /// <summary>
        /// 将对象生成字符源，写入PLC中
        /// </summary>
        /// <returns>准备写入到远程的数据</returns>
        byte[] ToSource();
    }
}
