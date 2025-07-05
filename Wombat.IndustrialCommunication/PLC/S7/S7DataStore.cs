using System;
using System.Collections.Generic;
using System.Text;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// S7数据存储，用于存储S7协议的数据区域
    /// </summary>
    public class S7DataStore
    {
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 数据写入事件
        /// </summary>
        public event EventHandler<S7DataStoreEventArgs> DataStoreWrittenTo;

        /// <summary>
        /// 数据读取事件
        /// </summary>
        public event EventHandler<S7DataStoreEventArgs> DataStoreReadFrom;

        /// <summary>
        /// 构造函数
        /// </summary>
        public S7DataStore()
        {
            // 默认构造函数
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dbSize">DB区大小</param>
        /// <param name="merkerSize">M区大小</param>
        /// <param name="inputSize">I区大小</param>
        /// <param name="outputSize">Q区大小</param>
        /// <param name="timerSize">T区大小</param>
        /// <param name="counterSize">C区大小</param>
        internal S7DataStore(int dbSize, int merkerSize, int inputSize, int outputSize, int timerSize, int counterSize)
        {
            DataBlocks = new Dictionary<int, MemoryLite<byte>>();
            Merkers = new MemoryLite<byte>(new byte[merkerSize], 0, merkerSize);
            Inputs = new MemoryLite<byte>(new byte[inputSize], 0, inputSize);
            Outputs = new MemoryLite<byte>(new byte[outputSize], 0, outputSize);
            Timers = new MemoryLite<byte>(new byte[timerSize], 0, timerSize);
            Counters = new MemoryLite<byte>(new byte[counterSize], 0, counterSize);
        }

        /// <summary>
        /// 数据块区域，键为DB编号，值为对应的数据
        /// </summary>
        public Dictionary<int, MemoryLite<byte>> DataBlocks { get; set; } = new Dictionary<int, MemoryLite<byte>>();

        /// <summary>
        /// 内部标志位区域（M区）
        /// </summary>
        public MemoryLite<byte> Merkers { get; set; }

        /// <summary>
        /// 输入区域（I区）
        /// </summary>
        public MemoryLite<byte> Inputs { get; set; }

        /// <summary>
        /// 输出区域（Q区）
        /// </summary>
        public MemoryLite<byte> Outputs { get; set; }

        /// <summary>
        /// 定时器区域（T区）
        /// </summary>
        public MemoryLite<byte> Timers { get; set; }

        /// <summary>
        /// 计数器区域（C区）
        /// </summary>
        public MemoryLite<byte> Counters { get; set; }

        /// <summary>
        /// 同步锁对象
        /// </summary>
        public object SyncRoot
        {
            get { return _syncRoot; }
        }

        /// <summary>
        /// 帮助方法，用于引发事件
        /// </summary>
        /// <typeparam name="T">事件参数类型</typeparam>
        /// <param name="handler">事件处理器</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">事件参数</param>
        private static void RaiseEvent<T>(EventHandler<T> handler, object sender, T args) where T : EventArgs
        {
            handler?.Invoke(sender, args);
        }

        /// <summary>
        /// 创建一个数据块
        /// </summary>
        /// <param name="dbNumber">DB编号</param>
        /// <param name="size">大小</param>
        public void CreateDataBlock(int dbNumber, int size)
        {
            lock (SyncRoot)
            {
                if (DataBlocks.ContainsKey(dbNumber))
                {
                    throw new ArgumentException($"数据块DB{dbNumber}已存在");
                }

                DataBlocks[dbNumber] = new MemoryLite<byte>(new byte[size], 0, size);
            }
        }

        /// <summary>
        /// 获取指定数据块
        /// </summary>
        /// <param name="dbNumber">DB编号</param>
        /// <returns>数据块</returns>
        public MemoryLite<byte> GetDataBlock(int dbNumber)
        {
            lock (SyncRoot)
            {
                if (!DataBlocks.ContainsKey(dbNumber))
                {
                    throw new ArgumentException($"数据块DB{dbNumber}不存在");
                }

                return DataBlocks[dbNumber];
            }
        }

        /// <summary>
        /// 删除指定数据块
        /// </summary>
        /// <param name="dbNumber">DB编号</param>
        public void DeleteDataBlock(int dbNumber)
        {
            lock (SyncRoot)
            {
                if (!DataBlocks.ContainsKey(dbNumber))
                {
                    throw new ArgumentException($"数据块DB{dbNumber}不存在");
                }

                DataBlocks.Remove(dbNumber);
            }
        }

        /// <summary>
        /// 读取指定区域的数据
        /// </summary>
        /// <param name="area">区域类型</param>
        /// <param name="dbNumber">DB编号（仅当area为DB时有效）</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="length">长度</param>
        /// <returns>读取的数据</returns>
        public byte[] ReadArea(S7Area area, int dbNumber, int startAddress, int length)
        {
            lock (SyncRoot)
            {
                MemoryLite<byte> memory = null;

                switch (area)
                {
                    case S7Area.DB:
                        if (!DataBlocks.ContainsKey(dbNumber))
                        {
                            throw new ArgumentException($"数据块DB{dbNumber}不存在");
                        }
                        memory = DataBlocks[dbNumber];
                        break;
                    case S7Area.M:
                        memory = Merkers;
                        break;
                    case S7Area.I:
                        memory = Inputs;
                        break;
                    case S7Area.Q:
                        memory = Outputs;
                        break;
                    case S7Area.T:
                        memory = Timers;
                        break;
                    case S7Area.C:
                        memory = Counters;
                        break;
                    default:
                        throw new ArgumentException($"不支持的区域类型: {area}");
                }

                if (startAddress < 0 || startAddress + length > memory.Size)
                {
                    throw new ArgumentOutOfRangeException(nameof(startAddress), "地址范围超出限制");
                }

                byte[] result = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    result[i] = memory[startAddress + i];
                }

                // 引发读取事件
                RaiseEvent(DataStoreReadFrom, this, S7DataStoreEventArgs.CreateReadEventArgs(area, dbNumber, startAddress, result));

                return result;
            }
        }

        /// <summary>
        /// 写入指定区域的数据
        /// </summary>
        /// <param name="area">区域类型</param>
        /// <param name="dbNumber">DB编号（仅当area为DB时有效）</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="data">要写入的数据</param>
        public void WriteArea(S7Area area, int dbNumber, int startAddress, byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            lock (SyncRoot)
            {
                MemoryLite<byte> memory = null;

                switch (area)
                {
                    case S7Area.DB:
                        if (!DataBlocks.ContainsKey(dbNumber))
                        {
                            throw new ArgumentException($"数据块DB{dbNumber}不存在");
                        }
                        memory = DataBlocks[dbNumber];
                        break;
                    case S7Area.M:
                        memory = Merkers;
                        break;
                    case S7Area.I:
                        memory = Inputs;
                        break;
                    case S7Area.Q:
                        memory = Outputs;
                        break;
                    case S7Area.T:
                        memory = Timers;
                        break;
                    case S7Area.C:
                        memory = Counters;
                        break;
                    default:
                        throw new ArgumentException($"不支持的区域类型: {area}");
                }

                if (startAddress < 0 || startAddress + data.Length > memory.Size)
                {
                    throw new ArgumentOutOfRangeException(nameof(startAddress), "地址范围超出限制");
                }

                for (int i = 0; i < data.Length; i++)
                {
                    memory[startAddress + i] = data[i];
                }

                // 引发写入事件
                RaiseEvent(DataStoreWrittenTo, this, S7DataStoreEventArgs.CreateWriteEventArgs(area, dbNumber, startAddress, data));
            }
        }
    }

    /// <summary>
    /// S7数据区域
    /// </summary>
    public enum S7Area : byte
    {
        /// <summary>
        /// 数据块
        /// </summary>
        DB = 0x84,

        /// <summary>
        /// 内部标志位
        /// </summary>
        M = 0x83,

        /// <summary>
        /// 输入
        /// </summary>
        I = 0x81,

        /// <summary>
        /// 输出
        /// </summary>
        Q = 0x82,

        /// <summary>
        /// 定时器
        /// </summary>
        T = 0x1D,

        /// <summary>
        /// 计数器
        /// </summary>
        C = 0x1C
    }
} 