using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Wombat.IndustrialCommunication.Iec104.Model;
using Wombat.IndustrialCommunication.Iec104.Protocol;
using Xunit;

namespace Wombat.IndustrialCommunication.Iec104.Tests
{
    public class PointTableTests : IDisposable
    {
        private readonly string _directory;

        public PointTableTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "iec104tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }

        private string WriteCsv(string name, string content)
        {
            var path = Path.Combine(_directory, name);
            File.WriteAllText(path, content, new UTF8Encoding(true));
            return path;
        }

        private string WriteTelemetry()
        {
            return WriteCsv("telemetry.csv", string.Join("\r\n", new[]
            {
                "转发号,通道,设备,测点描述,系数,偏移值,变化死区,ASDU类型",
                "0,保护装置,进线柜高压保护,频率,1,0,0,不带时标的短浮点数测量值",
                "1,保护装置,进线柜高压保护,线电压Uab,0.1,5,0,不带时标的短浮点数测量值",
            }));
        }

        private string WriteDiscrete()
        {
            return WriteCsv("discrete.csv", string.Join("\r\n", new[]
            {
                "转发号,通道,设备,测点描述,极性,是否转发SOE,是否转发COS,遥信类型",
                "100,保护装置,进线柜高压保护,开关合位,正极性,是,是,单点",
                "101,保护装置,进线柜高压保护,接地刀,反极性,否,是,单点",
            }));
        }

        private string WriteCommand()
        {
            return WriteCsv("command.csv", string.Join("\r\n", new[]
            {
                "转发号,通道,设备,测点描述",
                "200,保护装置,进线柜高压保护,保护合闸",
            }));
        }

        [Fact]
        public void LoadsThreeTables_WithOffsetAddressing()
        {
            var options = new Iec104PointTableOptions
            {
                TelemetryPath = WriteTelemetry(),
                DiscretePath = WriteDiscrete(),
                CommandPath = WriteCommand(),
                CommonAddress = 1,
                IoaBase = 1,
            };

            var table = Iec104PointTable.Load(options);

            Assert.Equal(5, table.Count);

            Iec104Point point;
            Assert.True(table.TryGetPoint(1, out point));
            Assert.Equal(Iec104TypeId.MeasuredFloat, point.TypeId);
            Assert.Equal(1d, point.Scale);

            Assert.True(table.TryGetPoint(2, out point));
            Assert.Equal(0.1d, point.Scale);
            Assert.Equal(5d, point.Offset);
            Assert.Equal(15d, point.ToEngineering(100d), 6);

            Assert.True(table.TryGetPoint(101, out point));
            Assert.Equal(Iec104TypeId.SinglePointWithTime, point.TypeId);
            Assert.True(point.IsSoe);

            Assert.True(table.TryGetPoint(102, out point));
            Assert.Equal(Iec104TypeId.SinglePoint, point.TypeId);
            Assert.True(point.Invert);

            Assert.True(table.TryGetPoint(201, out point));
            Assert.True(point.IsCommand);
            Assert.Equal(Iec104TypeId.SingleCommand, point.TypeId);

            Assert.Equal(2, table.GetPoints(Iec104TypeId.MeasuredFloat).Count);
        }

        [Fact]
        public void ZeroDeadband_ProducesWarning()
        {
            var options = new Iec104PointTableOptions { TelemetryPath = WriteTelemetry() };
            var table = Iec104PointTable.Load(options);

            Assert.Contains(table.Warnings, w => w.Contains("变化死区为 0"));
        }

        [Fact]
        public void CategoryBase_OverridesGlobalBase()
        {
            var options = new Iec104PointTableOptions
            {
                TelemetryPath = WriteTelemetry(),
                IoaBase = 1,
                TelemetryIoaBase = 5000,
            };

            var table = Iec104PointTable.Load(options);

            Iec104Point point;
            Assert.True(table.TryGetPoint(5000, out point));
            Assert.True(table.TryGetPoint(5001, out point));
            Assert.False(table.TryGetPoint(1, out point));
        }

        [Fact]
        public void OverlappingCategories_ProduceIoaCollisionWarning()
        {
            var telemetry = WriteCsv("t.csv", string.Join("\r\n", new[]
            {
                "转发号,通道,设备,测点描述,系数,偏移值,变化死区,ASDU类型",
                "0,保护装置,进线柜,频率,1,0,1,不带时标的短浮点数测量值",
            }));
            var discrete = WriteCsv("d.csv", string.Join("\r\n", new[]
            {
                "转发号,通道,设备,测点描述,极性,是否转发SOE,是否转发COS,遥信类型",
                "0,保护装置,进线柜,合位,正极性,否,是,单点",
            }));

            var table = Iec104PointTable.Load(new Iec104PointTableOptions
            {
                TelemetryPath = telemetry,
                DiscretePath = discrete,
            });

            Assert.Contains(table.Warnings, w => w.Contains("IOA 在不同类别间冲突"));
        }

        [Fact]
        public void DuplicateForwardNumber_ProducesWarning()
        {
            var path = WriteCsv("duplicate.csv", string.Join("\r\n", new[]
            {
                "转发号,通道,设备,测点描述,系数,偏移值,变化死区,ASDU类型",
                "7,保护装置,进线柜,频率,1,0,1,不带时标的短浮点数测量值",
                "7,保护装置,进线柜,电流,1,0,1,不带时标的短浮点数测量值",
            }));

            var table = Iec104PointTable.Load(new Iec104PointTableOptions { TelemetryPath = path });
            Assert.Contains(table.Warnings, w => w.Contains("重复"));
        }

        [Fact]
        public void DiscreteTypeOverride_ForcesSinglePoint()
        {
            var options = new Iec104PointTableOptions
            {
                DiscretePath = WriteDiscrete(),
                DiscreteTypeOverride = Iec104TypeId.SinglePoint,
            };

            var table = Iec104PointTable.Load(options);
            Assert.All(table.Points, p => Assert.Equal(Iec104TypeId.SinglePoint, p.TypeId));
        }
    }
}
