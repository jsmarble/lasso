using System;
using System.Globalization;

namespace Lasso
{
    public class HourlyUtcRedisKeyBuilder : IRedisKeyBuilder
    {
        public string BuildRedisKey(UsageRequest usageRequest)
        {
            return $"{DateTime.UtcNow.ToString("yyyyMMddHH", CultureInfo.InvariantCulture)}:{usageRequest.Context}";
        }
    }
}
