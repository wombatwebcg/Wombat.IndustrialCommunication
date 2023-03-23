using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.PLC;
using Xunit;
using Wombat.Infrastructure;
using Wombat.IndustrialCommunication;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
   public class SimpleSockClient_Tests
    {
        private SimpleSockClient client;
        string ip = "192.168.0.1";

        public SimpleSockClient_Tests()
        {

        }

        [Fact]
        //[InlineData(MitsubishiVersion.A_1E, 6001)]
        public void 短连接自动开关()
        {

            client = new SimpleSockClient(ip,501);
            client.Connect();
            var sss = client.SendPackageReliable(new byte[4] { 1, 2, 3, 4 });
            client.Disconnect();
        }


    }
}
