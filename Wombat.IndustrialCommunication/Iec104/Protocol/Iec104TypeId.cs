using System;
using System.Collections.Generic;

namespace Wombat.IndustrialCommunication.Iec104.Protocol
{
    /// <summary>
    /// 104 类型标识。仅列出本实现会用到以及需要识别长度的类型。
    /// </summary>
    public enum Iec104TypeId : byte
    {
        SinglePoint = 1,
        SinglePointWithTime24 = 2,
        DoublePoint = 3,
        DoublePointWithTime24 = 4,
        StepPosition = 5,
        BitString32 = 7,
        MeasuredNormalized = 9,
        MeasuredScaled = 11,
        MeasuredFloat = 13,
        IntegratedTotals = 15,
        SinglePointWithTime = 30,
        DoublePointWithTime = 31,
        StepPositionWithTime = 32,
        BitString32WithTime = 33,
        MeasuredNormalizedWithTime = 34,
        MeasuredScaledWithTime = 35,
        MeasuredFloatWithTime = 36,
        SingleCommand = 45,
        DoubleCommand = 46,
        StepCommand = 47,
        SetpointNormalized = 48,
        SetpointScaled = 49,
        SetpointFloat = 50,
        EndOfInitialization = 70,
        InterrogationCommand = 100,
        CounterInterrogationCommand = 101,
        ReadCommand = 102,
        ClockSynchronizationCommand = 103,
        TestCommand = 104,
        ResetProcessCommand = 105,
        DelayAcquisitionCommand = 106,
    }

    /// <summary>
    /// 类型元数据：元素长度（不含 3 字节信息对象地址）。
    /// </summary>
    public static class Iec104TypeInfo
    {
        private static readonly Dictionary<Iec104TypeId, int> ElementSizes = new Dictionary<Iec104TypeId, int>
        {
            { Iec104TypeId.SinglePoint, 1 },
            { Iec104TypeId.SinglePointWithTime24, 4 },
            { Iec104TypeId.DoublePoint, 1 },
            { Iec104TypeId.DoublePointWithTime24, 4 },
            { Iec104TypeId.StepPosition, 1 },
            { Iec104TypeId.BitString32, 5 },
            { Iec104TypeId.MeasuredNormalized, 3 },
            { Iec104TypeId.MeasuredScaled, 3 },
            { Iec104TypeId.MeasuredFloat, 5 },
            { Iec104TypeId.IntegratedTotals, 5 },
            { Iec104TypeId.SinglePointWithTime, 8 },
            { Iec104TypeId.DoublePointWithTime, 8 },
            { Iec104TypeId.StepPositionWithTime, 8 },
            { Iec104TypeId.BitString32WithTime, 12 },
            { Iec104TypeId.MeasuredNormalizedWithTime, 10 },
            { Iec104TypeId.MeasuredScaledWithTime, 10 },
            { Iec104TypeId.MeasuredFloatWithTime, 12 },
            { Iec104TypeId.SingleCommand, 1 },
            { Iec104TypeId.DoubleCommand, 1 },
            { Iec104TypeId.StepCommand, 1 },
            { Iec104TypeId.SetpointNormalized, 3 },
            { Iec104TypeId.SetpointScaled, 3 },
            { Iec104TypeId.SetpointFloat, 5 },
            { Iec104TypeId.EndOfInitialization, 1 },
            { Iec104TypeId.InterrogationCommand, 1 },
            { Iec104TypeId.CounterInterrogationCommand, 1 },
            { Iec104TypeId.ReadCommand, 0 },
            { Iec104TypeId.ClockSynchronizationCommand, 7 },
            { Iec104TypeId.TestCommand, 2 },
            { Iec104TypeId.ResetProcessCommand, 1 },
            { Iec104TypeId.DelayAcquisitionCommand, 2 },
        };

        /// <summary>获取元素长度；未知类型返回 false。</summary>
        public static bool TryGetElementSize(Iec104TypeId typeId, out int size)
        {
            return ElementSizes.TryGetValue(typeId, out size);
        }

        /// <summary>类型是否已知。</summary>
        public static bool IsKnown(Iec104TypeId typeId)
        {
            return ElementSizes.ContainsKey(typeId);
        }

        /// <summary>
        /// 类型是否携带信息对象地址。IEC 60870-5-104 的每个信息对象都带 3 字节地址，
        /// 站级命令的地址固定为 0，因此这里始终为 true。
        /// </summary>
        public static bool HasInformationObjectAddress(Iec104TypeId typeId)
        {
            return true;
        }

        /// <summary>
        /// 是否为站级命令：ASDU 中地址固定为 0，不含具体测点。
        /// </summary>
        public static bool IsStationCommand(Iec104TypeId typeId)
        {
            switch (typeId)
            {
                case Iec104TypeId.EndOfInitialization:
                case Iec104TypeId.InterrogationCommand:
                case Iec104TypeId.CounterInterrogationCommand:
                case Iec104TypeId.ClockSynchronizationCommand:
                case Iec104TypeId.TestCommand:
                case Iec104TypeId.ResetProcessCommand:
                case Iec104TypeId.DelayAcquisitionCommand:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>是否为遥控类型。</summary>
        public static bool IsCommand(Iec104TypeId typeId)
        {
            switch (typeId)
            {
                case Iec104TypeId.SingleCommand:
                case Iec104TypeId.DoubleCommand:
                case Iec104TypeId.StepCommand:
                case Iec104TypeId.SetpointNormalized:
                case Iec104TypeId.SetpointScaled:
                case Iec104TypeId.SetpointFloat:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>是否为带时标类型。</summary>
        public static bool HasTimeTag(Iec104TypeId typeId)
        {
            switch (typeId)
            {
                case Iec104TypeId.SinglePointWithTime:
                case Iec104TypeId.DoublePointWithTime:
                case Iec104TypeId.StepPositionWithTime:
                case Iec104TypeId.BitString32WithTime:
                case Iec104TypeId.MeasuredNormalizedWithTime:
                case Iec104TypeId.MeasuredScaledWithTime:
                case Iec104TypeId.MeasuredFloatWithTime:
                    return true;
                default:
                    return false;
            }
        }
    }
}
