using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.CoreTests
{
    public class ObservableArrayTests
    {

        [Fact]

        public async Task AddRangeAsyncShouldNotifyObservers()
        {
            await using var array = new ObservableArray<int>(1)
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
            array.Set(0, 999);
            Assert.Equal(999, array.Get(0));
        }
    }
}
