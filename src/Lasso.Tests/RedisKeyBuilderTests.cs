using StackExchange.Redis;
using System.Globalization;

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
            UsageRequest req = new UsageRequest(resource, context, quota);
            var key = builder.BuildRedisKey(req);
            string expected = $"{DateTime.UtcNow.ToString("yyyyMMddHH", CultureInfo.InvariantCulture)}:{context}";
            Assert.That(key, Is.EqualTo(expected));
        }

        [Test]
        public void Daily_Output_Expected()
        {
            DailyUtcRedisKeyBuilder builder = new DailyUtcRedisKeyBuilder();
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest(resource, context, quota);
            var key = builder.BuildRedisKey(req);
            string expected = $"{DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}:{context}";
            Assert.That(key, Is.EqualTo(expected));
        }

        [Test]
        public void Monthly_Output_Expected()
        {
            MonthlyUtcRedisKeyBuilder builder = new MonthlyUtcRedisKeyBuilder();
            string resource = "field_reindex";
            string context = Guid.NewGuid().ToString();
            int quota = 10;
            UsageRequest req = new UsageRequest(resource, context, quota);
            var key = builder.BuildRedisKey(req);
            string expected = $"{DateTime.UtcNow.ToString("yyyyMM01", CultureInfo.InvariantCulture)}:{context}";
            Assert.That(key, Is.EqualTo(expected));
        }

        [TestCase("ar-SA")]
        [TestCase("fa-IR")]
        [TestCase("th-TH")]
        public void Built_In_Builders_Use_Invariant_Calendar(string cultureName)
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                UsageRequest request = new UsageRequest("resource", "context", 10);

                AssertInvariantPeriod(new HourlyUtcRedisKeyBuilder(), request, "yyyyMMddHH");
                AssertInvariantPeriod(new DailyUtcRedisKeyBuilder(), request, "yyyyMMdd");
                AssertInvariantPeriod(new MonthlyUtcRedisKeyBuilder(), request, "yyyyMM01");
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        private static void AssertInvariantPeriod(IRedisKeyBuilder builder, UsageRequest request, string format)
        {
            DateTime before = DateTime.UtcNow;
            string key = builder.BuildRedisKey(request);
            DateTime after = DateTime.UtcNow;

            string expectedBefore = $"{before.ToString(format, CultureInfo.InvariantCulture)}:{request.Context}";
            string expectedAfter = $"{after.ToString(format, CultureInfo.InvariantCulture)}:{request.Context}";
            Assert.That(key, Is.EqualTo(expectedBefore).Or.EqualTo(expectedAfter));
        }
    }
}
