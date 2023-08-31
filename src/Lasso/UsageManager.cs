using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Data.Common;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace Lasso
{
    public class RedisUsageManager : IUsageManager, IDisposable
    {
        private volatile IConnectionMultiplexer connection;
        private volatile IDatabase cache;

        private bool disposedValue;
        private readonly SemaphoreSlim connectionLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        private readonly LassoOptions options;
        private readonly IRedisKeyBuilder keyBuilder;

        private readonly IFixedExpirationStrategy fixedExpirationStrategy;
        private readonly IRelativeExpirationStrategy relativeExpirationStrategy;
        private readonly ILogger logger;

        public RedisUsageManager(IOptions<LassoOptions> options, IRedisKeyBuilder redisKeyBuilder, IRelativeExpirationStrategy expirationStrategy, ILogger logger = null)
            : this(options, redisKeyBuilder, logger)
        {
            ArgumentNullThrowHelper.ThrowIfNull(expirationStrategy);
            this.relativeExpirationStrategy = expirationStrategy;
        }

        public RedisUsageManager(IOptions<LassoOptions> options, IRedisKeyBuilder redisKeyBuilder, IFixedExpirationStrategy expirationStrategy, ILogger logger = null)
            : this(options, redisKeyBuilder, logger)
        {
            ArgumentNullThrowHelper.ThrowIfNull(expirationStrategy);
            this.fixedExpirationStrategy = expirationStrategy;
        }

        private RedisUsageManager(IOptions<LassoOptions> options, IRedisKeyBuilder redisKeyBuilder, ILogger logger = null)
        {
            ArgumentNullThrowHelper.ThrowIfNull(options);
            ArgumentNullThrowHelper.ThrowIfNull(redisKeyBuilder);

            this.options = options.Value;
            this.keyBuilder = redisKeyBuilder;
            this.logger = logger ?? NullLoggerFactory.Instance.CreateLogger<RedisUsageManager>();
        }

        public async Task<UsageResult> GetAsync(UsageRequest req, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullThrowHelper.ThrowIfNull(req);

            await ConnectAsync(token).ConfigureAwait(false);

            string key = this.keyBuilder.BuildRedisKey(req);
            long current = (long)(await this.cache.HashGetAsync(key, req.Resource).ConfigureAwait(false));
            await SetExpirationAsync(key).ConfigureAwait(false);

            return new UsageResult(req.Resource, req.Context, req.Quota, current);
        }

        public async Task<UsageResult> IncrementAsync(UsageRequest req, long increment = 1, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullThrowHelper.ThrowIfNull(req);

            await ConnectAsync(token).ConfigureAwait(false);

            string key = this.keyBuilder.BuildRedisKey(req);
            long current = await this.cache.HashIncrementAsync(key, req.Resource, increment).ConfigureAwait(false);
            await SetExpirationAsync(key, token).ConfigureAwait(false);

            return new UsageResult(req.Resource, req.Context, req.Quota, current);
        }

        public async Task<UsageResult> DecrementAsync(UsageRequest req, long decrement = 1, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullThrowHelper.ThrowIfNull(req);

            await ConnectAsync(token).ConfigureAwait(false);

            string key = this.keyBuilder.BuildRedisKey(req);
            long current = await this.cache.HashDecrementAsync(key, req.Resource, decrement).ConfigureAwait(false);
            await SetExpirationAsync(key, token).ConfigureAwait(false);

            return new UsageResult(req.Resource, req.Context, req.Quota, current);
        }

        public async Task<UsageResult> ResetAsync(UsageRequest req, long init = 0, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullThrowHelper.ThrowIfNull(req);

            await ConnectAsync(token).ConfigureAwait(false);

            string key = this.keyBuilder.BuildRedisKey(req);
            await this.cache.HashSetAsync(key, req.Resource, init).ConfigureAwait(false);
            await SetExpirationAsync(key).ConfigureAwait(false);

            return new UsageResult(req.Resource, req.Context, req.Quota, init);
        }

        public async Task<DateTime?> GetExpirationAsync(UsageRequest req, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullThrowHelper.ThrowIfNull(req);

            await ConnectAsync(token).ConfigureAwait(false);

            string key = this.keyBuilder.BuildRedisKey(req);
            return await this.cache.KeyExpireTimeAsync(key).ConfigureAwait(false);
        }

        private async Task SetExpirationAsync(string key, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            await ConnectAsync(token).ConfigureAwait(false);

            if (relativeExpirationStrategy != null)
            {
                if (relativeExpirationStrategy.Expiration == TimeSpan.MaxValue)
                    await this.cache.KeyPersistAsync(key).ConfigureAwait(false);
                else
                    await this.cache.KeyExpireAsync(key, relativeExpirationStrategy.Expiration, relativeExpirationStrategy.Sliding ? ExpireWhen.Always : ExpireWhen.HasNoExpiry).ConfigureAwait(false);
            }
            else if (fixedExpirationStrategy != null)
            {
                if (fixedExpirationStrategy.Expiration == DateTime.MaxValue)
                    await this.cache.KeyPersistAsync(key).ConfigureAwait(false);
                else
                    await this.cache.KeyExpireAsync(key, fixedExpirationStrategy.Expiration).ConfigureAwait(false);
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

            await connectionLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (cache == null)
                {
                    if (options.ConnectionMultiplexer != null)
                        connection = options.ConnectionMultiplexer;
                    else if (options.RedisConfigurationOptions != null)
                        connection = await ConnectionMultiplexer.ConnectAsync(options.RedisConfigurationOptions);
                    else
                        connection = await ConnectionMultiplexer.ConnectAsync(options.RedisConfiguration);

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
                    connection.Close();
                    connection.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
