using System;
using System.IO;
using System.Text;
using Wombat.IndustrialCommunication.Extensions.IEC104.Model;
using Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge;
using Xunit;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Tests
{
    public class ModbusBridgeConfigurationTests : IDisposable
    {
        private readonly string _directory;

        public ModbusBridgeConfigurationTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "bridge-config-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            try { Directory.Delete(_directory, true); } catch { }
        }

        [Fact]
        public void ValidConfigurationShouldMapToOptions()
        {
            WriteCsv("telemetry.csv", "转发号,测点描述,ASDU类型\n0,遥测0,不带时标的短浮点数测量值\n1,遥测1,不带时标的短浮点数测量值\n");
            WriteCsv("discrete.csv", "转发号,测点描述,遥信类型\n0,遥信0,单点\n1,遥信1,单点\n");
            WriteCsv("command.csv", "转发号,测点描述\n0,遥控0\n");

            var path = WriteFile("bridge.json", @"{
  // 现场联调配置
  ""modbus"": { ""bindAddress"": ""127.0.0.1"", ""port"": 1502, ""unitId"": 3, ""healthBase"": 2000, ""wordOrder"": ""Cdab"", ""flushIntervalMs"": 20 },
  ""command"": { ""enabled"": true, ""mode"": ""Direct"", ""allowedIoa"": [1, 2], ""confirmTimeoutSeconds"": 10 },
  ""stale"": { ""readMode"": ""Exception"", ""timeoutSeconds"": 5 },
  ""iec104"": { ""host"": ""10.0.0.5"", ""port"": 2404, ""commonAddress"": 2, ""cyclicInterrogationSeconds"": 900 },
  ""pointTable"": { ""telemetryPath"": ""telemetry.csv"", ""discretePath"": ""discrete.csv"", ""commandPath"": ""command.csv"", ""ioaBase"": 1 }
}");

            var configuration = ModbusBridgeConfiguration.Load(path);

            Assert.Empty(configuration.Validate());
            Assert.Equal(1502, configuration.Modbus.Port);
            Assert.Equal(ModbusFloatWordOrder.Cdab, configuration.Modbus.WordOrder);

            var options = configuration.CreateOptions();
            Assert.True(options.EnableCommands);
            Assert.Equal(Iec104CommandMode.Direct, options.CommandMode);
            Assert.Equal(ModbusStaleReadMode.Exception, options.StaleReadMode);
            Assert.Equal(TimeSpan.FromMilliseconds(20), options.FlushInterval);
            Assert.Contains(1, options.AllowedCommandIoa);

            var table = configuration.LoadPointTable();
            Assert.Equal(5, table.Count);
            Assert.Equal(2, table.GetPoints(Wombat.IndustrialCommunication.Extensions.IEC104.Protocol.Iec104TypeId.MeasuredFloat).Count);

            var master = configuration.CreateMasterOptions(table);
            Assert.Equal("10.0.0.5", master.Host);
            Assert.Equal(2, master.CommonAddress);
            Assert.Equal(TimeSpan.FromMinutes(15), master.CyclicInterrogation);
            Assert.Same(table, master.PointTable);
        }

        [Fact]
        public void InvalidValuesShouldAllBeReported()
        {
            WriteCsv("telemetry.csv", "转发号,测点描述\n0,遥测0\n");

            var path = WriteFile("bridge.json", @"{
  ""modbus"": { ""port"": 0, ""unitId"": 300, ""discreteBase"": -1, ""flushIntervalMs"": 0 },
  ""command"": { ""confirmTimeoutSeconds"": 0, ""allowedIoa"": [0] },
  ""stale"": { ""timeoutSeconds"": 0 },
  ""iec104"": { ""host"": """", ""port"": 0, ""k"": 0, ""w"": 0 },
  ""pointTable"": { ""telemetryPath"": ""telemetry.csv"", ""discretePath"": ""missing.csv"" }
}");

            var configuration = ModbusBridgeConfiguration.Load(path);
            var errors = configuration.Validate();

            Assert.Contains(errors, message => message.Contains("modbus.port"));
            Assert.Contains(errors, message => message.Contains("modbus.unitId"));
            Assert.Contains(errors, message => message.Contains("modbus.discreteBase"));
            Assert.Contains(errors, message => message.Contains("modbus.flushIntervalMs"));
            Assert.Contains(errors, message => message.Contains("command.confirmTimeoutSeconds"));
            Assert.Contains(errors, message => message.Contains("command.allowedIoa"));
            Assert.Contains(errors, message => message.Contains("stale.timeoutSeconds"));
            Assert.Contains(errors, message => message.Contains("iec104.host"));
            Assert.Contains(errors, message => message.Contains("missing.csv"));

            var error = Assert.Throws<ModbusBridgeConfigurationException>(() => configuration.EnsureValid());
            Assert.Equal(errors.Count, error.Errors.Count);
        }

        [Fact]
        public void MalformedJsonShouldBeRejectedWithPosition()
        {
            var path = WriteFile("broken.json", "{ \"modbus\": { \"port\": } }");

            var error = Assert.Throws<ModbusBridgeConfigurationException>(() => ModbusBridgeConfiguration.Load(path));

            Assert.Contains(error.Errors, message => message.Contains("解析失败"));
        }

        [Fact]
        public void DuplicateForwardNoShouldBeRejectedByMap()
        {
            WriteCsv("telemetry.csv", "转发号,测点描述\n0,遥测0\n0,遥测1\n");

            var path = WriteFile("bridge.json", @"{
  ""modbus"": { ""port"": 1502 },
  ""pointTable"": { ""telemetryPath"": ""telemetry.csv"" }
}");

            var configuration = ModbusBridgeConfiguration.Load(path);
            Assert.Empty(configuration.Validate());

            var error = Assert.Throws<ModbusBridgeConfigurationException>(
                () => ModbusRegisterMap.Build(configuration.LoadPointTable(), configuration.CreateOptions()));

            Assert.Contains(error.Errors, message => message.Contains("重复"));
        }

        [Fact]
        public void AddressOverflowShouldBeRejected()
        {
            WriteCsv("telemetry.csv", "转发号,测点描述\n40000,遥测0\n");

            var path = WriteFile("bridge.json", @"{
  ""modbus"": { ""port"": 1502 },
  ""pointTable"": { ""telemetryPath"": ""telemetry.csv"" }
}");

            var configuration = ModbusBridgeConfiguration.Load(path);

            var error = Assert.Throws<ModbusBridgeConfigurationException>(
                () => ModbusRegisterMap.Build(configuration.LoadPointTable(), configuration.CreateOptions()));

            Assert.Contains(error.Errors, message => message.Contains("超出"));
        }

        private string WriteCsv(string name, string content)
        {
            return WriteFile(name, content);
        }

        private string WriteFile(string name, string content)
        {
            var path = Path.Combine(_directory, name);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }
    }
}
