using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Data.Common;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lasso
{
    public class RedisUsageManager : IUsageManager, IDisposable
    {
        private volatile ConnectionMultiplexer connection;
        private IDatabase cache;
        private bool disposedValue;
        private readonly SemaphoreSlim connectionLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        private readonly LassoOptions options;
        private readonly IRedisKeyBuilder keyBuilder;

        private readonly IFixedExpirationStrategy fixedExpirationStrategy;
        private readonly IRelativeExpirationStrategy relativeExpirationStrategy;
        private readonly ILogger logger;

        public RedisUsageManager(IOptions<LassoOptions> options, IRedisKeyBuilder redisKeyBuilder, IRelativeExpirationStrategy expirationStrategy, ILogger logger)
            : this(options, redisKeyBuilder)
        {
            if (expirationStrategy == null) throw new ArgumentNullException(nameof(expirationStrategy));
            this.relativeExpirationStrategy = expirationStrategy;
            this.logger = logger;
        }

        public RedisUsageManager(IOptions<LassoOptions> options, IRedisKeyBuilder redisKeyBuilder, IFixedExpirationStrategy expirationStrategy)
            : this(options, redisKeyBuilder)
        {
            if (expirationStrategy == null) throw new ArgumentNullException(nameof(expirationStrategy));
            this.fixedExpirationStrategy = expirationStrategy;
        }

        private RedisUsageManager(IOptions<LassoOptions> options, IRedisKeyBuilder redisKeyBuilder)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (redisKeyBuilder == null) throw new ArgumentNullException(nameof(redisKeyBuilder));

            this.options = options.Value;
            this.keyBuilder = redisKeyBuilder;
        }

        public async Task<UsageResult> GetAsync(UsageRequest req, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullThrowHelper.ThrowIfNull(req);

            await ConnectAsync(token);

            string key = this.keyBuilder.BuildRedisKey(req);
            long current = (long)(await this.cache.HashGetAsync(key, req.Resource));
            await SetExpirationAsync(key);

            return new UsageResult
            {
                Current = current,
                Resource = req.Resource,
                Quota = req.Quota,
                Context = req.Context,
            };
        }

        public async Task<UsageResult> IncrementAsync(UsageRequest req, long increment = 1, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullThrowHelper.ThrowIfNull(req);

            await ConnectAsync(token);

            string key = this.keyBuilder.BuildRedisKey(req);
            long current = await this.cache.HashIncrementAsync(key, req.Resource, increment);
            await SetExpirationAsync(key);

            return new UsageResult
            {
                Current = current,
                Resource = req.Resource,
                Quota = req.Quota,
                Context = req.Context,
            };
        }

        public async Task<UsageResult> DecrementAsync(UsageRequest req, long decrement = 1, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullThrowHelper.ThrowIfNull(req);

            await ConnectAsync(token);

            string key = this.keyBuilder.BuildRedisKey(req);
            long current = await this.cache.HashDecrementAsync(key, req.Resource, decrement);
            await SetExpirationAsync(key);

            return new UsageResult
            {
                Current = current,
                Resource = req.Resource,
                Quota = req.Quota,
                Context = req.Context,
            };
        }

        public async Task<UsageResult> ResetAsync(UsageRequest req, long init = 0, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullThrowHelper.ThrowIfNull(req);

            await ConnectAsync(token);

            string key = this.keyBuilder.BuildRedisKey(req);
            await this.cache.HashSetAsync(key, req.Resource, init);
            await SetExpirationAsync(key);

            return new UsageResult
            {
                Current = init,
                Resource = req.Resource,
                Quota = req.Quota,
                Context = req.Context,
            };
        }

        public async Task<DateTime?> GetExpirationAsync(UsageRequest req, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullThrowHelper.ThrowIfNull(req);

            await ConnectAsync(token);

            string key = this.keyBuilder.BuildRedisKey(req);
            return await this.cache.KeyExpireTimeAsync(key);
        }

        private async Task SetExpirationAsync(string key, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            await ConnectAsync(token);

            if (relativeExpirationStrategy != null)
            {
                if (relativeExpirationStrategy.Expiration == TimeSpan.MaxValue)
                    await this.cache.KeyPersistAsync(key);
                else
                    await this.cache.KeyExpireAsync(key, relativeExpirationStrategy.Expiration, relativeExpirationStrategy.Sliding ? ExpireWhen.Always : ExpireWhen.HasNoExpiry);
            }
            else if (fixedExpirationStrategy != null)
            {
                if (fixedExpirationStrategy.Expiration == DateTime.MaxValue)
                    await this.cache.KeyPersistAsync(key);
                else
                    await this.cache.KeyExpireAsync(key, fixedExpirationStrategy.Expiration);
            }
            else
                Trace.TraceWarning("No key expiration strategy provided. Usage keys will persist for the life of the cluster.");
        }

        private async Task ConnectAsync(CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            if (cache != null)
            {
                return;
            }

            await connectionLock.WaitAsync(token);
            try
            {
                if (cache == null)
                {
                    if (options.RedisConfigurationOptions != null)
                    {
                        connection = await ConnectionMultiplexer.ConnectAsync(options.RedisConfigurationOptions);
                    }
                    else
                    {
                        connection = await ConnectionMultiplexer.ConnectAsync(options.RedisConfiguration);
                    }

                    cache = connection.GetDatabase();
                }
            }
            finally
            {
                connectionLock.Release();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~RedisUsageManager()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
