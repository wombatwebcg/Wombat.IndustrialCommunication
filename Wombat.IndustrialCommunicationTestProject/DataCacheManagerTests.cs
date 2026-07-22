using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject
{
    public class DataCacheManagerTests
    {
        [Fact]
        public async Task CleanupRemovesExpiredAndOverLimitItems()
        {
            var cache = new DataCacheManager(new DataCacheConfig
            {
                CleanupInterval = 10,
                MaxCacheItems = 10
            });
            try
            {
                var key = new CacheKey("device", "address");
                await cache.GetOrAddAsync(key, _ => Task.FromResult(1), 1);
                Thread.Sleep(100);
                Assert.Equal(0, cache.GetCount());
            }
            finally
            {
                cache.Dispose();
            }

            cache = new DataCacheManager(new DataCacheConfig { MaxCacheItems = 0 });
            try
            {
                await cache.GetOrAddAsync(new CacheKey("device", "address"), _ => Task.FromResult(1), 10000);
                Assert.Equal(0, cache.GetCount());
            }
            finally
            {
                cache.Dispose();
            }
        }
    }
}
