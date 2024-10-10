using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Models
{
    public class SegmentedMemoryManager
    {
        private const int SegmentCount = 16;  // 假设分成16个段
        private readonly Dictionary<int, int>[] _segments;  // 每个段是一个独立的字典
        private readonly object[] _locks;  // 每个段有一个独立的锁

        public SegmentedMemoryManager()
        {
            _segments = new Dictionary<int, int>[SegmentCount];
            _locks = new object[SegmentCount];
            for (int i = 0; i < SegmentCount; i++)
            {
                _segments[i] = new Dictionary<int, int>();
                _locks[i] = new object();
            }
        }

        // 根据地址找到对应的段
        private int GetSegmentIndex(int address)
        {
            return address % SegmentCount;
        }

        // 获取数据
        public int GetData(int address)
        {
            int segmentIndex = GetSegmentIndex(address);
            lock (_locks[segmentIndex])  // 只锁定对应的段
            {
                return _segments[segmentIndex].ContainsKey(address) ? _segments[segmentIndex][address] : 0;
            }
        }

        // 设置数据
        public void SetData(int address, int value)
        {
            int segmentIndex = GetSegmentIndex(address);
            lock (_locks[segmentIndex])  // 只锁定对应的段
            {
                _segments[segmentIndex][address] = value;
            }
        }
    }
}
