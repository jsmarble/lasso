using StackExchange.Redis;

namespace Lasso
{
    public class RedisUsageManager : IUsageManager
    {
        private readonly IDatabase db;
        private readonly IRedisKeyBuilder keyBuilder;

        public RedisUsageManager(IConnectionMultiplexer muxer, IRedisKeyBuilder redisKeyBuilder)
        {
            this.db = muxer.GetDatabase();
            this.keyBuilder = redisKeyBuilder;
        }

        public async Task<UsageResult> GetAsync(UsageRequest req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            string key = this.keyBuilder.BuildRedisKey(req);
            long current = (long)(await this.db.HashGetAsync(key, req.Resource));
            return new UsageResult
            {
                Current = current,
                Resource = req.Resource,
                Quota = req.Quota,
                Context = req.Context,
            };
        }

        public async Task<UsageResult> IncrementAsync(UsageRequest req, long increment = 1)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            string key = this.keyBuilder.BuildRedisKey(req);
            long current = await this.db.HashIncrementAsync(key, req.Resource, increment);
            return new UsageResult
            {
                Current = current,
                Resource = req.Resource,
                Quota = req.Quota,
                Context = req.Context,
            };
        }

        public async Task<UsageResult> DecrementAsync(UsageRequest req, long decrement = 1)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            string key = this.keyBuilder.BuildRedisKey(req);
            long current = await this.db.HashDecrementAsync(key, req.Resource, decrement);
            return new UsageResult
            {
                Current = current,
                Resource = req.Resource,
                Quota = req.Quota,
                Context = req.Context,
            };
        }

        public async Task<UsageResult> ResetAsync(UsageRequest req, long init = 0)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            string key = this.keyBuilder.BuildRedisKey(req);
            await this.db.HashSetAsync(key, req.Resource, init);
            return new UsageResult
            {
                Current = init,
                Resource = req.Resource,
                Quota = req.Quota,
                Context = req.Context,
            };
        }
    }
}
