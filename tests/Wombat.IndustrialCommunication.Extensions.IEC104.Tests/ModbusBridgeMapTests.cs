using System;
using System.Collections.Generic;
using System.Linq;
using Wombat.IndustrialCommunication.Extensions.IEC104.Model;
using Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge;
using Wombat.IndustrialCommunication.Extensions.IEC104.Protocol;
using Xunit;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Tests
{
    public class ModbusBridgeMapTests
    {
        private const int TelemetryBase = 16385;
        private const int DiscreteBase = 1;
        private const int CommandBase = 24577;

        private static Iec104PointTable BuildTable(
            int telemetryCount = 818,
            int discreteCount = 298,
            int commandCount = 4,
            Func<int, int> telemetryForward = null,
            Func<int, int> discreteForward = null)
        {
            var points = new List<Iec104Point>();

            for (var i = 0; i < telemetryCount; i++)
            {
                points.Add(new Iec104Point
                {
                    Ioa = TelemetryBase + i,
                    ForwardNo = telemetryForward == null ? i : telemetryForward(i),
                    TypeId = Iec104TypeId.MeasuredFloat,
                    Description = "遥测" + i,
                });
            }

            for (var i = 0; i < discreteCount; i++)
            {
                points.Add(new Iec104Point
                {
                    Ioa = DiscreteBase + i,
                    ForwardNo = discreteForward == null ? i : discreteForward(i),
                    TypeId = Iec104TypeId.SinglePoint,
                    Description = "遥信" + i,
                });
            }

            for (var i = 0; i < commandCount; i++)
            {
                points.Add(new Iec104Point
                {
                    Ioa = CommandBase + i,
                    ForwardNo = i,
                    TypeId = Iec104TypeId.SingleCommand,
                    Description = "遥控" + i,
                    IsCommand = true,
                });
            }

            return Iec104PointTable.FromPoints(points);
        }

        private static ModbusBridgeOptions BuildOptions()
        {
            return new ModbusBridgeOptions
            {
                BindAddress = "127.0.0.1",
                Port = 1502,
                UnitId = 1,
                DiscreteBase = 0,
                TelemetryBase = 0,
                CommandBase = 0,
                HealthBase = 1000,
            };
        }

        [Fact]
        public void TelemetryShouldUseBasePlusForwardNoTimesTwo()
        {
            var map = ModbusRegisterMap.Build(BuildTable(), BuildOptions());

            Assert.Equal(818, map.TelemetryPoints.Count);
            Assert.Equal(0, map.TelemetryPoints[0].Address);
            Assert.Equal(2, map.TelemetryPoints[1].Address);
            Assert.Equal(1634, map.TelemetryPoints[817].Address);
            Assert.Equal(2, map.TelemetryPoints[0].RegisterCount);
        }

        [Fact]
        public void DiscreteShouldUseBasePlusForwardNo()
        {
            var map = ModbusRegisterMap.Build(BuildTable(), BuildOptions());

            Assert.Equal(298, map.DiscretePoints.Count);
            Assert.Equal(0, map.DiscretePoints[0].Address);
            Assert.Equal(297, map.DiscretePoints[297].Address);
            Assert.Equal(1, map.DiscretePoints[0].RegisterCount);
        }

        [Fact]
        public void CommandShouldUseBasePlusForwardNoTimesThree()
        {
            var map = ModbusRegisterMap.Build(BuildTable(), BuildOptions());

            Assert.Equal(4, map.CommandPoints.Count);
            Assert.Equal(0, map.CommandPoints[0].Address);
            Assert.Equal(3, map.CommandPoints[1].Address);
            Assert.Equal(6, map.CommandPoints[2].Address);
            Assert.Equal(9, map.CommandPoints[3].Address);
            Assert.Equal(3, map.CommandPoints[0].RegisterCount);
        }

        [Fact]
        public void CommandAndHealthRegionsShouldNotOverlap()
        {
            var options = BuildOptions();
            options.CommandBase = 995;

            var error = Assert.Throws<ModbusBridgeConfigurationException>(
                () => ModbusRegisterMap.Build(BuildTable(), options));

            Assert.Contains(error.Errors, message => message.Contains("重叠"));
        }

        [Fact]
        public void DuplicateForwardNoShouldBeRejected()
        {
            var table = BuildTable(telemetryCount: 4, discreteCount: 0, commandCount: 0,
                telemetryForward: i => i == 3 ? 1 : i);

            var error = Assert.Throws<ModbusBridgeConfigurationException>(
                () => ModbusRegisterMap.Build(table, BuildOptions()));

            Assert.Contains(error.Errors, message => message.Contains("重复"));
        }

        [Fact]
        public void NegativeForwardNoShouldBeRejected()
        {
            var table = BuildTable(telemetryCount: 2, discreteCount: 0, commandCount: 0,
                telemetryForward: i => i == 1 ? -1 : i);

            var error = Assert.Throws<ModbusBridgeConfigurationException>(
                () => ModbusRegisterMap.Build(table, BuildOptions()));

            Assert.Contains(error.Errors, message => message.Contains("转发号为负"));
        }

        [Fact]
        public void AddressBeyondDataStoreRangeShouldBeRejected()
        {
            var table = BuildTable(telemetryCount: 1, discreteCount: 0, commandCount: 0,
                telemetryForward: _ => 40000);

            var error = Assert.Throws<ModbusBridgeConfigurationException>(
                () => ModbusRegisterMap.Build(table, BuildOptions()));

            Assert.Contains(error.Errors, message => message.Contains("超出"));
        }

        [Fact]
        public void NonContiguousForwardNoShouldLeaveGapsAndWarn()
        {
            var table = BuildTable(telemetryCount: 2, discreteCount: 0, commandCount: 0,
                telemetryForward: i => i == 1 ? 5 : 0);

            var map = ModbusRegisterMap.Build(table, BuildOptions());

            Assert.Equal(0, map.TelemetryPoints[0].Address);
            Assert.Equal(10, map.TelemetryPoints[1].Address);
            Assert.Contains(map.Warnings, warning => warning.Contains("空隙"));
        }

        [Fact]
        public void UnsupportedPointTypeShouldBeRejected()
        {
            var points = new List<Iec104Point>
            {
                new Iec104Point
                {
                    Ioa = 1,
                    ForwardNo = 0,
                    TypeId = Iec104TypeId.BitString32,
                    Description = "位串",
                },
            };

            var error = Assert.Throws<ModbusBridgeConfigurationException>(
                () => ModbusRegisterMap.Build(Iec104PointTable.FromPoints(points), BuildOptions()));

            Assert.Contains(error.Errors, message => message.Contains("无法映射"));
        }

        [Fact]
        public void MapVersionShouldBeStableAndReflectChanges()
        {
            var first = ModbusRegisterMap.Build(BuildTable(), BuildOptions());
            var second = ModbusRegisterMap.Build(BuildTable(), BuildOptions());
            var changed = ModbusRegisterMap.Build(BuildTable(telemetryForward: i => i * 2), BuildOptions());

            Assert.Equal(first.MapVersion, second.MapVersion);
            Assert.NotEqual(first.MapVersion, changed.MapVersion);
        }

        [Fact]
        public void ExportShouldCoverAllMappedPoints()
        {
            var map = ModbusRegisterMap.Build(BuildTable(), BuildOptions());

            var rows = map.Export();

            Assert.Equal(818 + 298 + 4, rows.Count);
            Assert.Equal(map.HealthBase, BuildOptions().HealthBase);
        }

        [Fact]
        public void FloatShouldRoundTripInBothWordOrders()
        {
            var values = new[]
            {
                0f, -0f, 1f, -1.5f, 123.456f, -9876.543f,
                float.MaxValue, float.MinValue, float.Epsilon,
                float.NaN, float.PositiveInfinity, float.NegativeInfinity,
            };

            foreach (var value in values)
            {
                foreach (var order in new[] { ModbusFloatWordOrder.Abcd, ModbusFloatWordOrder.Cdab })
                {
                    ushort first;
                    ushort second;
                    ModbusFloatCodec.Encode(value, order, out first, out second);

                    var decoded = ModbusFloatCodec.Decode(first, second, order);
                    Assert.Equal(BitsOf(value), BitsOf(decoded));
                }
            }
        }

        [Fact]
        public void AbcdShouldPutHighWordFirst()
        {
            ushort first;
            ushort second;
            ModbusFloatCodec.Encode(1f, ModbusFloatWordOrder.Abcd, out first, out second);

            Assert.Equal(0x3F80, first);
            Assert.Equal(0x0000, second);
        }

        [Fact]
        public void CdabShouldPutLowWordFirst()
        {
            ushort first;
            ushort second;
            ModbusFloatCodec.Encode(1f, ModbusFloatWordOrder.Cdab, out first, out second);

            Assert.Equal(0x0000, first);
            Assert.Equal(0x3F80, second);
        }

        private static int BitsOf(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }
    }
}
