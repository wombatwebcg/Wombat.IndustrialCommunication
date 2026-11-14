using System;
using System.Linq;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Iec104.Model;
using Wombat.IndustrialCommunication.Iec104.Protocol;
using Xunit;

namespace Wombat.IndustrialCommunication.Iec104.Tests
{
    public class MasterTests
    {
        [Fact]
        public async Task Connect_PerformsStartDataTransfer()
        {
            await using var harness = new MasterHarness();
            await harness.Master.ConnectAsync();

            Assert.True(harness.Master.IsConnected);
        }

        [Fact]
        public async Task Interrogation_CollectsAllValuesAndCompletes()
        {
            await using var harness = new MasterHarness();
            await harness.Master.ConnectAsync();

            var result = await harness.Master.InterrogateAsync();

            Assert.True(result.Completed);
            Assert.False(result.TimedOut);
            Assert.True(result.Confirmed);
            Assert.Equal(3, result.ReceivedCount);

            Iec104Value value;
            Assert.True(harness.Master.TryGetValue(1, out value));
            Assert.Equal(50.5d, value.Numeric, 3);

            Assert.True(harness.Master.TryGetValue(2, out value));
            Assert.Equal(220.25d, value.Numeric, 3);

            Assert.True(harness.Master.TryGetValue(101, out value));
            Assert.True(value.Boolean);
            Assert.True(value.HasTimestamp);
            Assert.Equal(new DateTime(2026, 11, 13, 10, 20, 30, 500), value.Timestamp);
        }

        [Fact]
        public async Task Interrogation_ReportsMissingPointsFromPointTable()
        {
            var table = Iec104PointTable.FromPoints(new[]
            {
                new Iec104Point { Ioa = 1, TypeId = Iec104TypeId.MeasuredFloat, Description = "A" },
                new Iec104Point { Ioa = 2, TypeId = Iec104TypeId.MeasuredFloat, Description = "B" },
                new Iec104Point { Ioa = 3, TypeId = Iec104TypeId.MeasuredFloat, Description = "C" },
            });

            await using var harness = new MasterHarness(pointTable: table);
            await harness.Master.ConnectAsync();

            var result = await harness.Master.InterrogateAsync();

            Assert.Equal(3, result.ExpectedCount);
            Assert.Equal(3, result.ReceivedCount);
            Assert.Single(result.MissingIoa);
            Assert.Equal(3, result.MissingIoa[0]);
        }

        [Fact]
        public async Task SingleCommand_DirectExecution_ReturnsAccepted()
        {
            await using var harness = new MasterHarness();
            await harness.Master.ConnectAsync();

            var result = await harness.Master.SingleCommandAsync(201, true);

            Assert.True(result.Accepted);
            Assert.False(result.Negative);
            Assert.Equal(Iec104CommandStage.Termination, result.Stage);
        }

        [Fact]
        public async Task SingleCommand_SelectAndExecute_SendsTwoFrames()
        {
            await using var harness = new MasterHarness();
            await harness.Master.ConnectAsync();

            var result = await harness.Master.SingleCommandAsync(201, true, Iec104CommandMode.SelectAndExecute);

            Assert.True(result.Accepted);

            var commands = harness.Slave.Received.Where(a => a.TypeId == Iec104TypeId.SingleCommand).ToList();
            Assert.Equal(2, commands.Count);
            Assert.True((commands[0].Objects[0].Element[0] & 0x80) != 0);
            Assert.True((commands[1].Objects[0].Element[0] & 0x80) == 0);
        }

        [Fact]
        public async Task SingleCommand_Rejected_ReturnsNegative()
        {
            await using var harness = new MasterHarness(rejectCommands: true);
            await harness.Master.ConnectAsync();

            var result = await harness.Master.SingleCommandAsync(201, true);

            Assert.False(result.Accepted);
            Assert.True(result.Negative);
        }

        [Fact]
        public async Task Interrogation_WithoutResponse_TimesOut()
        {
            await using var harness = new MasterHarness();
            await harness.Master.ConnectAsync();

            harness.Link.Dispose();

            await Assert.ThrowsAnyAsync<Exception>(() => harness.Master.InterrogateAsync());
        }
    }
}
