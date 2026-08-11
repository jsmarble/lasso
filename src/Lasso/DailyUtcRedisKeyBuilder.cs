using System;
using System.Globalization;

namespace Lasso
{
    public class DailyUtcRedisKeyBuilder : IRedisKeyBuilder
    {
        public string BuildRedisKey(UsageRequest usageRequest)
        {
            return $"{DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}:{usageRequest.Context}";
        }
    }
}
