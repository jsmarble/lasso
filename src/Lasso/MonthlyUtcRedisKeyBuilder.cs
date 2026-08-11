using System;
using System.Globalization;

namespace Lasso
{
    public class MonthlyUtcRedisKeyBuilder : IRedisKeyBuilder
    {
        public string BuildRedisKey(UsageRequest usageRequest)
        {
            return $"{DateTime.UtcNow.ToString("yyyyMM01", CultureInfo.InvariantCulture)}:{usageRequest.Context}";
        }
    }
}
