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
            var array = new ObservableArray<int>(100000)
            {
                //BatchInterval = TimeSpan.FromMilliseconds(10),
                //EnableBatchNotification = true
            };

            array.OnElementChanged += (index, oldValue, newValue) =>
            {
                Debug.WriteLine($"Index {index} changed from {oldValue} to {newValue},{DateTime.Now.ToString("HH:mm:ss ffff")}");

            };

            //array.OnBatchChanged += batchChanges =>
            //{
            //    Debug.WriteLine($"Batch Update: {batchChanges.Count} elements changed,{DateTime.Now.ToString("HH:mm:ss ffff")}");
            //};

            //Parallel.For(0, 100000, i =>
            //{
            //    array.Set(i, i * 10);
            //    array.Set(i, i * 20);
            //});

            for (int i = 0; i < 100000; i++)
            {
                array.Set(i, i * 10);

            }
            //Console.WriteLine($"Latest value at index 5: {array.GetLatest(5)}");

            Thread.Sleep(70000);
            array.StopWatching();
        }
    }
}
