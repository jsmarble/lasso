using StackExchange.Redis;
using System.Diagnostics;

namespace Lasso.Tests
{
    public class RedisUsageManagerTests
    {
        IConnectionMultiplexer muxer;

        [SetUp]
        public void Setup()
        {
            //Need a working Redis server to connect to
            muxer = ConnectionMultiplexer.Connect("10.0.2.12:6379");
        }

        [Test]
        public void Get_Returns_Correct_Value()
        {
            RedisUsageManager usageManager = new RedisUsageManager(muxer, new DailyUtcRedisKeyBuilder());
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest
            {
                Resource = resource,
                Quota = quota,
                Context = context
            };
            long inc = 10;
            var result = usageManager.IncrementAsync(req, inc).Result;
            Assert.That(result.Current, Is.EqualTo(inc));

            result = usageManager.GetAsync(req).Result;
            Assert.That(result.Current, Is.EqualTo(inc));
        }

        [Test]
        public void Increment_Works_No_Data()
        {
            RedisUsageManager usageManager = new RedisUsageManager(muxer, new DailyUtcRedisKeyBuilder());
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest
            {
                Resource = resource,
                Quota = quota,
                Context = context
            };
            var result = usageManager.IncrementAsync(req).Result;
            Assert.That(result.Current, Is.EqualTo(1));
        }

        [Test]
        public void Increment_Zero_Does_Not_Change_Value()
        {
            RedisUsageManager usageManager = new RedisUsageManager(muxer, new DailyUtcRedisKeyBuilder());
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest
            {
                Resource = resource,
                Quota = quota,
                Context = context
            };
            long inc = 10;
            var result = usageManager.IncrementAsync(req, inc).Result;
            Assert.That(result.Current, Is.EqualTo(inc));

            result = usageManager.IncrementAsync(req, 0).Result;
            Assert.That(result.Current, Is.EqualTo(inc));
        }

        [Test]
        public void Decrement_Works_No_Data()
        {
            RedisUsageManager usageManager = new RedisUsageManager(muxer, new DailyUtcRedisKeyBuilder());
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest
            {
                Resource = resource,
                Quota = quota,
                Context = context
            };
            var result = usageManager.DecrementAsync(req).Result;
            Assert.That(result.Current, Is.EqualTo(-1));
        }

        [Test]
        public void Reset_Works_No_Data()
        {
            RedisUsageManager usageManager = new RedisUsageManager(muxer, new DailyUtcRedisKeyBuilder());
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest
            {
                Resource = resource,
                Quota = quota,
                Context = context
            };
            var result = usageManager.ResetAsync(req).Result;
            Assert.That(result.Current, Is.EqualTo(0));
        }

        [Test]
        public void Reset_Sets_Value_To_Zero()
        {
            RedisUsageManager usageManager = new RedisUsageManager(muxer, new DailyUtcRedisKeyBuilder());
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest
            {
                Resource = resource,
                Quota = quota,
                Context = context
            };

            var result = usageManager.IncrementAsync(req, 10).Result;
            Assert.That(result.Current, Is.EqualTo(10));

            result = usageManager.ResetAsync(req).Result;
            Assert.That(result.Current, Is.EqualTo(0));
        }

        [Test]
        public void One_Thousand_Increments_Perf_Test()
        {
            RedisUsageManager usageManager = new RedisUsageManager(muxer, new DailyUtcRedisKeyBuilder());
            string resource = "send_message";
            string context = Guid.NewGuid().ToString();
            int quota = 1200;
            UsageRequest req = new UsageRequest
            {
                Resource = resource,
                Quota = quota,
                Context = context
            };

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
            TimeSpan max_acceptable = TimeSpan.FromSeconds(1);
            Assert.That(sw.Elapsed, Is.LessThan(max_acceptable));
        }

        [Test]
        public void Get_Throws_ArgumentNullExcept_On_Null_Request()
        {
            RedisUsageManager usageManager = new RedisUsageManager(muxer, new DailyUtcRedisKeyBuilder());
            Assert.ThrowsAsync<ArgumentNullException>(async () => await usageManager.GetAsync(null));
        }

        [Test]
        public void Increment_Throws_ArgumentNullExcept_On_Null_Request()
        {
            RedisUsageManager usageManager = new RedisUsageManager(muxer, new DailyUtcRedisKeyBuilder());
            Assert.ThrowsAsync<ArgumentNullException>(async () => await usageManager.IncrementAsync(null));
        }

        [Test]
        public void Decrement_Throws_ArgumentNullExcept_On_Null_Request()
        {
            RedisUsageManager usageManager = new RedisUsageManager(muxer, new DailyUtcRedisKeyBuilder());
            Assert.ThrowsAsync<ArgumentNullException>(async () => await usageManager.DecrementAsync(null));
        }

        [Test]
        public void Reset_Throws_ArgumentNullExcept_On_Null_Request()
        {
            RedisUsageManager usageManager = new RedisUsageManager(muxer, new DailyUtcRedisKeyBuilder());
            Assert.ThrowsAsync<ArgumentNullException>(async () => await usageManager.ResetAsync(null));
        }
    }
}