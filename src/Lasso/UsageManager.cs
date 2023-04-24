using StackExchange.Redis;
using System.Diagnostics;

namespace Lasso
{
    public class RedisUsageManager : IUsageManager
    {
        private readonly IDatabase db;
        private readonly IRedisKeyBuilder keyBuilder;

        private readonly IFixedExpirationStrategy fixedExpirationStrategy;
        private readonly IRelativeExpirationStrategy relativeExpirationStrategy;

        public RedisUsageManager(IConnectionMultiplexer muxer, IRedisKeyBuilder redisKeyBuilder, IRelativeExpirationStrategy expirationStrategy)
            : this(muxer, redisKeyBuilder)
        {
            if (expirationStrategy == null) throw new ArgumentNullException(nameof(expirationStrategy));
            this.relativeExpirationStrategy = expirationStrategy;
        }

        public RedisUsageManager(IConnectionMultiplexer muxer, IRedisKeyBuilder redisKeyBuilder, IFixedExpirationStrategy expirationStrategy)
            : this(muxer, redisKeyBuilder)
        {
            if (expirationStrategy == null) throw new ArgumentNullException(nameof(expirationStrategy));
            this.fixedExpirationStrategy = expirationStrategy;
        }

        private RedisUsageManager(IConnectionMultiplexer muxer, IRedisKeyBuilder redisKeyBuilder)
        {
            this.db = muxer.GetDatabase();
            this.keyBuilder = redisKeyBuilder;
        }

        public async Task<UsageResult> GetAsync(UsageRequest req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            string key = this.keyBuilder.BuildRedisKey(req);
            long current = (long)(await this.db.HashGetAsync(key, req.Resource));
            await SetExpirationAsync(key);

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
            await SetExpirationAsync(key);

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
            await SetExpirationAsync(key);

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
            await SetExpirationAsync(key);

            return new UsageResult
            {
                Current = init,
                Resource = req.Resource,
                Quota = req.Quota,
                Context = req.Context,
            };
        }

        public async Task<DateTime?> GetExpirationAsync(UsageRequest req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            string key = this.keyBuilder.BuildRedisKey(req);
            return await this.db.KeyExpireTimeAsync(key);
        }

        private async Task SetExpirationAsync(string key)
        {
            if (relativeExpirationStrategy != null)
            {
                if (relativeExpirationStrategy.Expiration == TimeSpan.MaxValue)
                    await this.db.KeyPersistAsync(key);
                else
                    await this.db.KeyExpireAsync(key, relativeExpirationStrategy.Expiration, relativeExpirationStrategy.Sliding ? ExpireWhen.Always : ExpireWhen.HasNoExpiry);
            }
            else if (fixedExpirationStrategy != null)
            {
                if (fixedExpirationStrategy.Expiration == DateTime.MaxValue)
                    await this.db.KeyPersistAsync(key);
                else
                    await this.db.KeyExpireAsync(key, fixedExpirationStrategy.Expiration);
            }
            else
                Trace.TraceWarning("No key expiration strategy provided. Usage keys will persist for the life of the cluster.");
        }
    }
}
