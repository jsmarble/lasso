namespace Lasso
{
    public interface IRedisKeyBuilder
    {
        string BuildRedisKey(UsageRequest usageRequest);
    }
}