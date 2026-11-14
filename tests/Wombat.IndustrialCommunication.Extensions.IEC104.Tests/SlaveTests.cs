using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Extensions.IEC104.Model;
using Wombat.IndustrialCommunication.Extensions.IEC104.Protocol;
using Xunit;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Tests
{
    public class SlaveTests
    {
        private static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMilliseconds = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                if (predicate()) return;
                await Task.Delay(20);
            }

            throw new TimeoutException("等待条件超时。");
        }

        [Fact]
        public async Task Interrogation_ReturnsAll1120Points()
        {
            await using var harness = await SlaveHarness.CreateAsync();

            harness.Slave.SetValue(SlaveHarness.TelemetryBase, 50.5);
            harness.Slave.SetSinglePoint(SlaveHarness.DiscreteBase, true);

            var result = await harness.Master.InterrogateAsync();

            Assert.True(result.Completed);
            Assert.False(result.TimedOut);
            Assert.Equal(1116, result.ReceivedCount);
            Assert.Equal(1116, result.ExpectedCount);
            Assert.Empty(result.MissingIoa);

            Iec104Value value;
            Assert.True(harness.Master.TryGetValue(SlaveHarness.TelemetryBase, out value));
            Assert.Equal(50.5d, value.Numeric, 3);

            Assert.True(harness.Master.TryGetValue(SlaveHarness.DiscreteBase, out value));
            Assert.True(value.Boolean);
        }

        [Fact]
        public async Task SpontaneousChange_ReachesMaster()
        {
            await using var harness = await SlaveHarness.CreateAsync();
            await harness.Master.InterrogateAsync();

            var ioa = SlaveHarness.TelemetryBase + 10;
            Assert.True(harness.Slave.SetValue(ioa, 123.456));

            await WaitUntilAsync(() =>
            {
                Iec104Value value;
                return harness.Master.TryGetValue(ioa, out value) && Math.Abs(value.Numeric - 123.456) < 0.001;
            });
        }

        [Fact]
        public async Task SoeChange_CarriesTimestamp()
        {
            await using var harness = await SlaveHarness.CreateAsync();
            await harness.Master.InterrogateAsync();

            var ioa = SlaveHarness.DiscreteBase + 5;
            harness.Slave.SetSinglePoint(ioa, true);

            await WaitUntilAsync(() =>
            {
                Iec104Value value;
                return harness.Master.TryGetValue(ioa, out value) && value.Boolean;
            });

            Iec104Value received;
            Assert.True(harness.Master.TryGetValue(ioa, out received));
            Assert.True(received.HasTimestamp);
            Assert.Equal(Iec104TypeId.SinglePointWithTime, received.TypeId);
            Assert.True(received.Timestamp > DateTime.Now.AddMinutes(-1));
        }

        [Fact]
        public async Task Command_Accepted_ReturnsTermination()
        {
            await using var harness = await SlaveHarness.CreateAsync();
            await harness.Master.InterrogateAsync();

            var result = await harness.Master.SingleCommandAsync(SlaveHarness.CommandBase, true);

            Assert.True(result.Accepted);
            Assert.False(result.Negative);
            Assert.Equal(Iec104CommandStage.Termination, result.Stage);
        }

        [Fact]
        public async Task Command_RejectedByHandler_ReturnsNegative()
        {
            await using var harness = await SlaveHarness.CreateAsync();
            harness.Slave.CommandRequested += (_, e) => e.Cancel = true;
            await harness.Master.InterrogateAsync();

            var result = await harness.Master.SingleCommandAsync(SlaveHarness.CommandBase, true);

            Assert.False(result.Accepted);
            Assert.True(result.Negative);
        }

        [Fact]
        public async Task SelectBeforeOperate_RejectsDirectExecute()
        {
            await using var harness = await SlaveHarness.CreateAsync(requireSelectBeforeOperate: true);
            await harness.Master.InterrogateAsync();

            var direct = await harness.Master.SingleCommandAsync(SlaveHarness.CommandBase, true);
            Assert.False(direct.Accepted);
            Assert.True(direct.Negative);

            var selected = await harness.Master.SingleCommandAsync(
                SlaveHarness.CommandBase, true, Iec104CommandMode.SelectAndExecute);
            Assert.True(selected.Accepted);
        }

        [Fact]
        public async Task CyclicUpload_ReachesMaster()
        {
            await using var harness = await SlaveHarness.CreateAsync(cyclicPeriod: TimeSpan.FromMilliseconds(200));
            await harness.Master.InterrogateAsync();

            var ioa = SlaveHarness.TelemetryBase + 20;
            harness.Slave.SetValue(ioa, 77.7);

            await WaitUntilAsync(() =>
            {
                Iec104Value value;
                return harness.Master.TryGetValue(ioa, out value) && Math.Abs(value.Numeric - 77.7) < 0.001;
            });
        }
    }
}
