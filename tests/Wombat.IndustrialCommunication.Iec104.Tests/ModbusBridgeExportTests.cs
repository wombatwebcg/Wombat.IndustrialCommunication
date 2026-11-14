using System;
using System.IO;
using System.Linq;
using Wombat.IndustrialCommunication.Iec104.ModbusBridge;
using Xunit;

namespace Wombat.IndustrialCommunication.Iec104.Tests
{
    public class ModbusBridgeExportTests
    {
        [Fact]
        public void ExportCsvShouldContainEveryMappedPoint()
        {
            var table = SlaveHarness.BuildTable();
            var map = ModbusRegisterMap.Build(table, new ModbusBridgeOptions
            {
                BindAddress = "127.0.0.1",
                Port = 1502,
                DiscreteBase = 0,
                TelemetryBase = 0,
                CommandBase = 0,
                HealthBase = 1000,
            });

            var lines = map.ExportCsv()
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(818 + 298 + 4 + 1, lines.Length);
            Assert.StartsWith("区域,地址,寄存器数,转发号,IOA,类型,描述", lines[0]);
            Assert.Contains(lines, line => line.StartsWith("离散输入,1000,1,1000,"));
            Assert.Contains(lines, line => line.StartsWith("输入寄存器,0,2,0,"));
            Assert.Equal(4, map.VersionText.Length);
        }

        [Fact]
        public void ExportCsvShouldEscapeCommasAndQuotes()
        {
            var points = new System.Collections.Generic.List<Model.Iec104Point>
            {
                new Model.Iec104Point
                {
                    Ioa = 1,
                    ForwardNo = 0,
                    TypeId = Protocol.Iec104TypeId.SinglePoint,
                    Description = "含,逗号与\"引号\"的遥信",
                },
            };

            var map = ModbusRegisterMap.Build(
                Model.Iec104PointTable.FromPoints(points),
                new ModbusBridgeOptions { BindAddress = "127.0.0.1", Port = 1502, HealthBase = 1000 });

            var csv = map.ExportCsv();

            Assert.Contains("\"含,逗号与\"\"引号\"\"的遥信\"", csv);
        }

        [Fact]
        public void ExportCsvShouldWriteFile()
        {
            var table = SlaveHarness.BuildTable();
            var map = ModbusRegisterMap.Build(table, new ModbusBridgeOptions
            {
                BindAddress = "127.0.0.1",
                Port = 1502,
                HealthBase = 1000,
            });

            var path = Path.Combine(Path.GetTempPath(), "bridge-map-" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                map.ExportCsv(path);
                Assert.True(File.Exists(path));
                Assert.Contains("离散输入", File.ReadAllText(path));
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }
    }
}
