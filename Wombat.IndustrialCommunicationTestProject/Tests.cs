using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject
{
    public class Tests
    {

        [Fact]

        public void ObservableArrayTest()
        {
            int count = 100_000;
            var array = new ObservableArray<int>(count)
            {
                ThrottleInterval = TimeSpan.FromMilliseconds(100),
                MaxThrottleInterval = TimeSpan.FromMilliseconds(500),
                EnableDynamicThrottling = true
                //BatchInterval = TimeSpan.FromMilliseconds(200),
                //EnableBatchNotification = true
            };
            //array.MarkHighPriority(0);

            array.OnElementChanged += (index, oldValue, newValue) =>
            {
                Debug.WriteLine($"Index {index} changed from {oldValue} to {newValue},{DateTime.Now.ToString("HH:mm:ss ffff")}");

            };

            //array.OnBatchChanged += batchChanges =>
            //{
            //    Debug.WriteLine($"Batch Update: {batchChanges.Count} elements changed,{DateTime.Now.ToString("HH:mm:ss ffff")}");
            //};

            //Parallel.For(0, count, i =>
            //{
            //    array.Set(i, i * 10);
            //    array.Set(i, i * 20);
            //});
            Task.Run(() =>
            {
                while(true)
                {
                    Random random = new Random();
                    for (int i = 0; i < count; i++)
                    {
                        //array.Set(i, random.Next(1,count) * 10);
                    }

                }
            });
            //array.Set(0, 999);
            //Thread.Sleep(70000);
            //array.StopWatching();
            for (; ; );
        }
    }
}
