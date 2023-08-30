using System;

namespace Lasso
{
    public class HourlyUtcRedisKeyBuilder : IRedisKeyBuilder
    {
        public string BuildRedisKey(UsageRequest usageRequest)
        {
            return $"{DateTime.UtcNow.ToString("yyyyMMddHH")}:{usageRequest.Context}";
        }
    }
}
