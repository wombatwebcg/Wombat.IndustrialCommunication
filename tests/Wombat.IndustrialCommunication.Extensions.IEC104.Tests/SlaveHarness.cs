using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Extensions.IEC104;
using Wombat.IndustrialCommunication.Extensions.IEC104.Model;
using Wombat.IndustrialCommunication.Extensions.IEC104.Options;
using Wombat.IndustrialCommunication.Extensions.IEC104.Protocol;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Tests
{
    /// <summary>从站加主站的真实 TCP 回环夹具。</summary>
    internal sealed class SlaveHarness : IAsyncDisposable
    {
        public const int TelemetryCount = 818;
        public const int DiscreteCount = 298;
        public const int CommandCount = 4;
        public const int TelemetryBase = 16385;
        public const int DiscreteBase = 1;
        public const int CommandBase = 24577;

        private SlaveHarness(Iec104PointTable table, Iec104Slave slave, Iec104Master master)
        {
            Table = table;
            Slave = slave;
            Master = master;
        }

        public Iec104PointTable Table { get; }

        public Iec104Slave Slave { get; }

        public Iec104Master Master { get; }

        public static Iec104PointTable BuildTable()
        {
            var points = new List<Iec104Point>();

            for (var i = 0; i < TelemetryCount; i++)
            {
                points.Add(new Iec104Point
                {
                    Ioa = TelemetryBase + i,
                    ForwardNo = i,
                    TypeId = Iec104TypeId.MeasuredFloat,
                    Description = "遥测" + i,
                    DataType = DataTypeEnums.Float,
                    Deadband = 0d,
                });
            }

            for (var i = 0; i < DiscreteCount; i++)
            {
                points.Add(new Iec104Point
                {
                    Ioa = DiscreteBase + i,
                    ForwardNo = 1000 + i,
                    TypeId = Iec104TypeId.SinglePointWithTime,
                    Description = "遥信" + i,
                    DataType = DataTypeEnums.Bool,
                    IsSoe = true,
                });
            }

            for (var i = 0; i < CommandCount; i++)
            {
                points.Add(new Iec104Point
                {
                    Ioa = CommandBase + i,
                    ForwardNo = 2000 + i,
                    TypeId = Iec104TypeId.SingleCommand,
                    Description = "遥控" + i,
                    DataType = DataTypeEnums.Bool,
                    IsCommand = true,
                });
            }

            return Iec104PointTable.FromPoints(points);
        }

        public static async Task<SlaveHarness> CreateAsync(
            bool requireSelectBeforeOperate = false,
            TimeSpan? cyclicPeriod = null)
        {
            var table = BuildTable();

            var slave = new Iec104Slave(new Iec104SlaveOptions
            {
                BindAddress = "127.0.0.1",
                Port = 0,
                CommonAddress = 1,
                PointTable = table,
                SendEndOfInitialization = true,
                RequireSelectBeforeOperate = requireSelectBeforeOperate,
                CyclicPeriod = cyclicPeriod ?? TimeSpan.Zero,
                T1 = TimeSpan.FromSeconds(3),
                T2 = TimeSpan.FromSeconds(1),
                T3 = TimeSpan.FromSeconds(15),
            });

            await slave.StartAsync();

            var endPoint = slave.LocalEndPoint;
            var master = new Iec104Master(new Iec104MasterOptions
            {
                Host = "127.0.0.1",
                Port = endPoint.Port,
                CommonAddress = 1,
                PointTable = table,
                T1 = TimeSpan.FromSeconds(3),
                T2 = TimeSpan.FromSeconds(1),
                T3 = TimeSpan.FromSeconds(15),
                InterrogationTimeout = TimeSpan.FromSeconds(10),
                CommandTimeout = TimeSpan.FromSeconds(3),
            });

            await master.ConnectAsync();
            return new SlaveHarness(table, slave, master);
        }

        public async ValueTask DisposeAsync()
        {
            await Master.DisposeAsync();
            await Slave.DisposeAsync();
        }
    }
}
