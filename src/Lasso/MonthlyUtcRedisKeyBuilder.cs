namespace Lasso
{
    public class MonthlyUtcRedisKeyBuilder : IRedisKeyBuilder
    {
        public string BuildRedisKey(UsageRequest usageRequest)
        {
            return $"{DateTime.UtcNow.Date.ToString("yyyyMM01")}:{usageRequest.Context}";
        }
    }
}