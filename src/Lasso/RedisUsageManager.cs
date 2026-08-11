using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace Lasso
{
    public class RedisUsageManager : IUsageManager, IDisposable
    {
        private volatile IConnectionMultiplexer ownedConnection;
        private volatile IDatabase cache;

        private bool disposedValue;
        private readonly SemaphoreSlim connectionLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        private readonly LassoOptions options;
        private readonly IRedisKeyBuilder keyBuilder;

        private readonly IFixedExpirationStrategy fixedExpirationStrategy;
        private readonly IRelativeExpirationStrategy relativeExpirationStrategy;
        private readonly ILogger logger;

        private const string MissingConnectionConfigurationMessage = "A Redis connection must be configured with ConnectionMultiplexer, RedisConfigurationOptions, or RedisConfiguration.";

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
            if (!HasConnectionConfiguration(this.options))
                throw new OptionsValidationException(Options.DefaultName, typeof(LassoOptions), new[] { MissingConnectionConfigurationMessage });

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

        public async Task<bool> DeleteAsync(UsageRequest req, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullThrowHelper.ThrowIfNull(req);

            await ConnectAsync(token).ConfigureAwait(false);

            string key = this.keyBuilder.BuildRedisKey(req);
            var deleted = await this.cache.HashDeleteAsync(key, req.Resource).ConfigureAwait(false);
            await SetExpirationAsync(key).ConfigureAwait(false);

            return deleted;
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
        }

        private void CheckDisposed()
        {
            ObjectDisposedThrowHelper.ThrowIf(disposedValue, this);
        }

        private ValueTask<IDatabase> ConnectAsync(CancellationToken token = default)
        {
            CheckDisposed();
            token.ThrowIfCancellationRequested();

            var cache = this.cache;
            if (cache != null)
            {
                Debug.Assert(this.cache != null);
                return new ValueTask<IDatabase>(cache);
            }
            return ConnectSlowAsync(token);
        }

        private async ValueTask<IDatabase> ConnectSlowAsync(CancellationToken token)
        {
            await connectionLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var cache = this.cache;
                if (cache is null)
                {
                    IConnectionMultiplexer connection;
                    bool ownsConnection;
                    if (options.ConnectionMultiplexer != null)
                    {
                        connection = options.ConnectionMultiplexer;
                        ownsConnection = false;
                    }
                    else if (options.RedisConfigurationOptions != null)
                    {
                        connection = await ConnectionMultiplexer.ConnectAsync(options.RedisConfigurationOptions);
                        ownsConnection = true;
                    }
                    else
                    {
                        connection = await ConnectionMultiplexer.ConnectAsync(options.RedisConfiguration);
                        ownsConnection = true;
                    }

                    try
                    {
                        cache = connection.GetDatabase();
                    }
                    catch
                    {
                        if (ownsConnection)
                            ReleaseConnection(connection);
                        throw;
                    }

                    if (ownsConnection)
                        this.ownedConnection = connection;
                    this.cache = cache;
                }
                Debug.Assert(this.cache != null);
                return cache;
            }
            finally
            {
                connectionLock.Release();
            }
        }


        private static bool HasConnectionConfiguration(LassoOptions options)
        {
            return options.ConnectionMultiplexer != null
                || options.RedisConfigurationOptions != null
                || !string.IsNullOrWhiteSpace(options.RedisConfiguration);
        }

        private void ReleaseConnection(IConnectionMultiplexer connection)
        {
            if (connection != null)
            {
                try
                {
                    connection.Close();
                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "An error occurred while disposing the Redis connection.");
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Interlocked.Exchange(ref this.cache, null);
                    ReleaseConnection(Interlocked.Exchange(ref this.ownedConnection, null));
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
