using StackExchange.Redis;

namespace Lasso.Tests
{
    public class RedisKeyBuilderTests
    {
        [Test]
        public void Hourly_Output_Expected()
        {
            HourlyUtcRedisKeyBuilder builder = new HourlyUtcRedisKeyBuilder();
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest
            {
                Resource = resource,
                Quota = quota,
                Context = context
            };
            var key = builder.BuildRedisKey(req);
            string expected = $"{DateTime.UtcNow.ToString("yyyyMMddHH")}:{context}";
            Assert.That(key, Is.EqualTo(expected));
        }

        [Test]
        public void Daily_Output_Expected()
        {
            DailyUtcRedisKeyBuilder builder = new DailyUtcRedisKeyBuilder();
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest
            {
                Resource = resource,
                Quota = quota,
                Context = context
            };
            var key = builder.BuildRedisKey(req);
            string expected = $"{DateTime.UtcNow.Date.ToString("yyyyMMdd")}:{context}";
            Assert.That(key, Is.EqualTo(expected));
        }

        [Test]
        public void Monthly_Output_Expected()
        {
            MonthlyUtcRedisKeyBuilder builder = new MonthlyUtcRedisKeyBuilder();
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest
            {
                Resource = resource,
                Quota = quota,
                Context = context
            };
            var key = builder.BuildRedisKey(req);
            string expected = $"{DateTime.UtcNow.Date.ToString("yyyyMM01")}:{context}";
            Assert.That(key, Is.EqualTo(expected));
        }
    }
}