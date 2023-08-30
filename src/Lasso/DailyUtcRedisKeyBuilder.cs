using System;

namespace Lasso
{
    public class DailyUtcRedisKeyBuilder : IRedisKeyBuilder
    {
        public string BuildRedisKey(UsageRequest usageRequest)
        {
            return $"{DateTime.UtcNow.Date.ToString("yyyyMMdd")}:{usageRequest.Context}";
        }
    }
}
