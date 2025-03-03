using System;
using System.Net.Sockets;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.PLC;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            //using (TcpClientAdapter client = new TcpClientAdapter("192.168.2.41", 102))
            //{
            //    var connect = client.Connect();
            //    S7EthernetTransport s7EthernetTransport = new S7EthernetTransport(client);
            //    S7Communication s7Communication = new S7Communication(s7EthernetTransport);
            //    s7Communication.SiemensVersion = SiemensVersion.S7_1200;
            //    var init = s7Communication.InitAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            //    //var uu = s7Communication.ReadByte("DB20.DBB10", 10);
            //    var u3 = s7Communication.ReadBoolean("DB21.DBX0.0", 112);
            //    var uu2 = s7Communication.Write("DB21.DBX0.0", false);
            //    var uu1 = s7Communication.ReadByte("DB20.DBB10");
            //    Console.WriteLine("Hello World!");
            //}

            //using (SiemensClient siemensClient = new SiemensClient("192.168.2.41", 102,SiemensVersion.S7_1200))
            //{
            //    var connect = siemensClient.Connect();
            //    var uu = siemensClient.ReadByte("DB20.DBB10", 10);
            //    var uu2 = siemensClient.Write("DB20.DBB10", (byte)1);
            //    var uu1 = siemensClient.ReadByte("DB20.DBB10");
            //    Console.WriteLine("Hello World!");
            //}

            //using (TcpClientAdapter client = new TcpClientAdapter("127.0.0.1", 502))
            //{
            //    var connect = client.Connect();
            //    DeviceMessageTransport s7EthernetTransport = new DeviceMessageTransport(client);
            //    ModbusTcp s7Communication = new ModbusTcp(s7EthernetTransport);
            //    while(true)
            //    {
            //        //var uu2 = s7Communication.Write($"1;15;0",new bool[15] {true,false,true,true,true , true, false, true, true, true, true, false, true, true, true });

            //        var uu2 = s7Communication.Write("1;5;1", false);
            //        var uu3 = s7Communication.ReadBoolean("1;1;0", 30);

            //        var uu = s7Communication.WriteAsync("1;16;0", new ushort[5] { 2,3,4,5,6});
            //        var uu1 = s7Communication.Write("1;16;1", 123);


            //    }
            //    Console.WriteLine("Hello World!");
            //}


            using (SerialPortAdapter client = new SerialPortAdapter("com1"))
            {
                var connect = client.Connect();
                DeviceMessageTransport s7EthernetTransport = new DeviceMessageTransport(client);
                s7EthernetTransport.StreamResource.ReceiveTimeout = TimeSpan.FromMilliseconds(100);
                s7EthernetTransport.StreamResource.SendTimeout = TimeSpan.FromMilliseconds(100);

                ModbusRTU s7Communication = new ModbusRTU(s7EthernetTransport);
                while (true)
                {
                    //var uu2 = s7Communication.Write($"1;15;0", new bool[15] { true, false, true, true, true, true, false, true, true, true, true, false, true, true, true });

                    var read1 = s7Communication.ReadInt16("1;3;0", 50);

                    //var uu = s7Communication.Write("1;16;0", new ushort[5] { 2, 3, 4, 5, 6 });
                    var uu1 = s7Communication.Write("1;16;1", (ushort)123);
                    Console.WriteLine(uu1.Requsts[0]+"\r\n");
                    Console.WriteLine(uu1.Responses[0] + "\r\n");

                }
                Console.WriteLine("Hello World!");
            }

        }
    }
}
