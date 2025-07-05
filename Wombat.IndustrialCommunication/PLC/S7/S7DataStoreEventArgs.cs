using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// S7数据存储事件参数
    /// </summary>
    public class S7DataStoreEventArgs : EventArgs
    {
        private S7DataStoreEventArgs(S7Area area, int dbNumber, int startAddress, int length)
        {
            this.Area = area;
            this.DbNumber = dbNumber;
            this.StartAddress = startAddress;
            this.Length = length;
        }

        /// <summary>
        /// 数据区域类型
        /// </summary>
        public S7Area Area { get; private set; }

        /// <summary>
        /// 数据块编号（仅对DB区域有效）
        /// </summary>
        public int DbNumber { get; private set; }

        /// <summary>
        /// 起始地址
        /// </summary>
        public int StartAddress { get; private set; }

        /// <summary>
        /// 数据长度
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// 数据内容
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public ReadOnlyCollection<byte> Data { get; private set; }

        /// <summary>
        /// 操作类型
        /// </summary>
        public S7DataOperationType OperationType { get; private set; }

        /// <summary>
        /// 创建读取事件参数
        /// </summary>
        /// <param name="area">数据区域</param>
        /// <param name="dbNumber">数据块编号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="data">读取的数据</param>
        /// <returns>事件参数</returns>
        internal static S7DataStoreEventArgs CreateReadEventArgs(S7Area area, int dbNumber, int startAddress, byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var eventArgs = new S7DataStoreEventArgs(area, dbNumber, startAddress, data.Length)
            {
                Data = new ReadOnlyCollection<byte>(data),
                OperationType = S7DataOperationType.Read
            };

            return eventArgs;
        }

        /// <summary>
        /// 创建写入事件参数
        /// </summary>
        /// <param name="area">数据区域</param>
        /// <param name="dbNumber">数据块编号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="data">写入的数据</param>
        /// <returns>事件参数</returns>
        internal static S7DataStoreEventArgs CreateWriteEventArgs(S7Area area, int dbNumber, int startAddress, byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var eventArgs = new S7DataStoreEventArgs(area, dbNumber, startAddress, data.Length)
            {
                Data = new ReadOnlyCollection<byte>(data),
                OperationType = S7DataOperationType.Write
            };

            return eventArgs;
        }
    }

    /// <summary>
    /// S7数据操作类型
    /// </summary>
    public enum S7DataOperationType
    {
        /// <summary>
        /// 读取操作
        /// </summary>
        Read,

        /// <summary>
        /// 写入操作
        /// </summary>
        Write
    }
} 