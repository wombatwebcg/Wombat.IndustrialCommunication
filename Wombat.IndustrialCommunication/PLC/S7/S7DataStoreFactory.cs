using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// S7数据存储工厂，用于创建S7DataStore实例
    /// </summary>
    public static class S7DataStoreFactory
    {
        // 默认的M区大小 (8KB)
        private const int DEFAULT_MERKER_SIZE = 8192;
        // 默认的I区大小 (8KB)
        private const int DEFAULT_INPUT_SIZE = 8192;
        // 默认的Q区大小 (8KB)
        private const int DEFAULT_OUTPUT_SIZE = 8192;
        // 默认的T区大小 (2KB)
        private const int DEFAULT_TIMER_SIZE = 2048;
        // 默认的C区大小 (2KB)
        private const int DEFAULT_COUNTER_SIZE = 2048;

        /// <summary>
        /// 创建默认大小的S7数据存储
        /// </summary>
        /// <returns>S7数据存储</returns>
        public static S7DataStore CreateDefaultDataStore()
        {
            return CreateDataStore(0, DEFAULT_MERKER_SIZE, DEFAULT_INPUT_SIZE, DEFAULT_OUTPUT_SIZE, DEFAULT_TIMER_SIZE, DEFAULT_COUNTER_SIZE);
        }

        /// <summary>
        /// 创建自定义大小的S7数据存储
        /// </summary>
        /// <param name="dbSize">DB区大小（无效，DB区是按需创建的）</param>
        /// <param name="merkerSize">M区大小</param>
        /// <param name="inputSize">I区大小</param>
        /// <param name="outputSize">Q区大小</param>
        /// <param name="timerSize">T区大小</param>
        /// <param name="counterSize">C区大小</param>
        /// <returns>S7数据存储</returns>
        public static S7DataStore CreateDataStore(int dbSize, int merkerSize, int inputSize, int outputSize, int timerSize, int counterSize)
        {
            return new S7DataStore(dbSize, merkerSize, inputSize, outputSize, timerSize, counterSize);
        }

        /// <summary>
        /// 创建测试用的S7数据存储（带有预设数据）
        /// </summary>
        /// <returns>S7数据存储</returns>
        internal static S7DataStore CreateTestDataStore()
        {
            S7DataStore dataStore = CreateDefaultDataStore();
            
            // 创建一些默认的数据块
            dataStore.CreateDataBlock(1, 1024);  // DB1 1KB
            dataStore.CreateDataBlock(2, 2048);  // DB2 2KB
            dataStore.CreateDataBlock(3, 4096);  // DB3 4KB
            
            // 填充一些测试数据
            FillTestData(dataStore);
            
            return dataStore;
        }
        
        /// <summary>
        /// 填充测试数据
        /// </summary>
        /// <param name="dataStore">S7数据存储</param>
        private static void FillTestData(S7DataStore dataStore)
        {
            // 在DB1中填充一些测试数据
            if (dataStore.DataBlocks.ContainsKey(1))
            {
                var db1 = dataStore.DataBlocks[1];
                for (int i = 0; i < 100; i++)
                {
                    db1[i] = (byte)(i % 256);
                }
            }
            
            // 在M区填充一些测试数据
            for (int i = 0; i < 100; i++)
            {
                dataStore.Merkers[i] = (byte)(i % 256);
            }
            
            // 在I区填充一些测试数据
            for (int i = 0; i < 100; i++)
            {
                dataStore.Inputs[i] = (byte)((i * 2) % 256);
            }
            
            // 在Q区填充一些测试数据
            for (int i = 0; i < 100; i++)
            {
                dataStore.Outputs[i] = (byte)((i * 3) % 256);
            }
        }
    }
} 