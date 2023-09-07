using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Diagnostics;

namespace Lasso.Tests
{
    public class RedisUsageManagerTests
    {
        IConnectionMultiplexer muxer;
        IRedisKeyBuilder keyBuilder;
        IRelativeExpirationStrategy expirationStrategy;
        IOptions<LassoOptions> options;

        const string REDIS_URI = "127.0.0.1:6379";

        [SetUp]
        public void Setup()
        {
            //Need a working Redis server to connect to
            muxer = ConnectionMultiplexer.Connect(REDIS_URI);
            options = Options.Create<LassoOptions>(new LassoOptions { ConnectionMultiplexer = muxer });
            keyBuilder = new DailyUtcRedisKeyBuilder();
            expirationStrategy = new TimeSpanExpirationStrategy(TimeSpan.FromHours(1), false);
        }

        [Test]
        public void Get_Returns_Correct_Value()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest(resource, context, quota);
            long inc = 10;
            var result = usageManager.IncrementAsync(req, inc).Result;
            Assert.That(result.Current, Is.EqualTo(inc));

            result = usageManager.GetAsync(req).Result;
            Assert.That(result.Current, Is.EqualTo(inc));
        }

        [Test]
        public void Increment_Works_No_Data()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest(resource, context, quota);
            var result = usageManager.IncrementAsync(req).Result;
            Assert.That(result.Current, Is.EqualTo(1));
        }

        [Test]
        public void Increment_Zero_Does_Not_Change_Value()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest(resource, context, quota);
            long inc = 10;
            var result = usageManager.IncrementAsync(req, inc).Result;
            Assert.That(result.Current, Is.EqualTo(inc));

            result = usageManager.IncrementAsync(req, 0).Result;
            Assert.That(result.Current, Is.EqualTo(inc));
        }

        [Test]
        public void Decrement_Works_No_Data()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest(resource, context, quota);
            var result = usageManager.DecrementAsync(req).Result;
            Assert.That(result.Current, Is.EqualTo(-1));
        }

        [Test]
        public void Delete_Works_No_Data()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest(resource, context, quota);
            var result = usageManager.DeleteAsync(req).Result;
            Assert.That(result, Is.False);
        }

        [Test]
        public void Delete_Works_With_Data()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest(resource, context, quota);
            var result = usageManager.IncrementAsync(req).Result;
            Assert.That(result.Current, Is.EqualTo(1));
            bool del = usageManager.DeleteAsync(req).Result;
            Assert.That(del, Is.True);
        }

        [Test]
        public void Reset_Works_No_Data()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest(resource, context, quota);
            var result = usageManager.ResetAsync(req).Result;
            Assert.That(result.Current, Is.EqualTo(0));
        }

        [Test]
        public void Reset_Sets_Value_To_Zero()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest(resource, context, quota);

            var result = usageManager.IncrementAsync(req, 10).Result;
            Assert.That(result.Current, Is.EqualTo(10));

            result = usageManager.ResetAsync(req).Result;
            Assert.That(result.Current, Is.EqualTo(0));
        }

        [Test]
        public void One_Thousand_Increments_Perf_Test()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            string resource = "send_message";
            string context = Guid.NewGuid().ToString();
            int quota = 1200;
            UsageRequest req = new UsageRequest(resource, context, quota);

            int iterations = 1000;
            long lastResult = 0;
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var result = usageManager.IncrementAsync(req).Result;
                Assert.That(result.Current, Is.Not.EqualTo(lastResult));
                lastResult = result.Current;
            }
            sw.Stop();

            Assert.That(lastResult, Is.EqualTo(iterations));
            TimeSpan max_acceptable = TimeSpan.FromSeconds(2);
            Assert.That(sw.Elapsed, Is.Not.EqualTo(TimeSpan.Zero));
            Assert.That(sw.Elapsed, Is.LessThan(max_acceptable));
        }

        [Test]
        public void Get_Throws_ArgumentNullExcept_On_Null_Request()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            Assert.ThrowsAsync<ArgumentNullException>(async () => await usageManager.GetAsync(null));
        }

        [Test]
        public void Increment_Throws_ArgumentNullExcept_On_Null_Request()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            Assert.ThrowsAsync<ArgumentNullException>(async () => await usageManager.IncrementAsync(null));
        }

        [Test]
        public void Decrement_Throws_ArgumentNullExcept_On_Null_Request()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            Assert.ThrowsAsync<ArgumentNullException>(async () => await usageManager.DecrementAsync(null));
        }

        [Test]
        public void Reset_Throws_ArgumentNullExcept_On_Null_Request()
        {
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
            Assert.ThrowsAsync<ArgumentNullException>(async () => await usageManager.ResetAsync(null));
        }

        [Test]
        public async Task GetExpiration_Returns_Valid_Result()
        {
            IRelativeExpirationStrategy expStr = new TimeSpanExpirationStrategy(TimeSpan.FromSeconds(88), false);
            RedisUsageManager usageManager = new RedisUsageManager(options, keyBuilder, expStr);
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest(resource, context, quota);
            DateTime minValidExpiration = DateTime.UtcNow.Add(expStr.Expiration).AddSeconds(-5);
            DateTime maxValidExpiration = DateTime.UtcNow.Add(expStr.Expiration).AddSeconds(5);

            var result = await usageManager.IncrementAsync(req);
            Assert.That(result.Current, Is.EqualTo(1));
            var exp = await usageManager.GetExpirationAsync(req);

            Assert.That(exp, Is.AtMost(maxValidExpiration));
            Assert.That(exp, Is.AtLeast(minValidExpiration));
        }
    }
}
