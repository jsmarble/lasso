using System;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Lasso.Extensions.DependencyInjection
{
    public static class LassoServiceDependencyInjectionExtensions
    {
        /// <summary>
        /// Registers services required by Lass0.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns>A <see cref="LassoServiceBuilder"/> that can be used to further configure Lasso.</returns>
        public static LassoServiceBuilder AddLasso(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.TryAddSingleton<IUsageManager, RedisUsageManager>();

            return new LassoServiceBuilder(services);
        }
        public static LassoServiceBuilder AddLasso(this IServiceCollection services, Action<LassoConfigOptions> configureOptionsFactory)
        {
            var builder = services.AddLasso();

            //*** Josh - the connection to the Redis DB - may want to consider moving it to the implementation of the IUsageManager (RedisUsageManager), maybe constructor (see RedisCache from MS).
            // Then we can just register the options class as per commented line below and the RedisUsageManager can implement the same logic as we do here to start the connection
            // This is also how MS is doing it with their OOO redis implementation - see RedisCache.cs in .net github repo

            //services.Configure(configureOptions);
            LassoConfigOptions configureOptions = new LassoConfigOptions();
            configureOptionsFactory(configureOptions);
            IConnectionMultiplexer? connection = null;
            if (configureOptions.RedisConfigurationOptions is not null)
            {
                connection = ConnectionMultiplexer.Connect(configureOptions.RedisConfigurationOptions);
            }
            else if(!string.IsNullOrWhiteSpace(configureOptions.RedisConfiguration))
            {
                connection = ConnectionMultiplexer.Connect(configureOptions.RedisConfiguration);
            }
            if (connection != null)
            {
                services.AddSingleton<IConnectionMultiplexer>(connection);
            }
            

            //*** Josh - this is an example
            //services.AddStackExchangeRedisCache(options =>
            //{
            //    options.ConfigurationOptions
            //});


            return builder;
        }

        public static LassoServiceBuilder AddLasso(this IServiceCollection services,
            Action<LassoConfigOptions> configureOptionsFactory,
            IRedisKeyBuilder keyBuilder,
            IRelativeExpirationStrategy relativeExpirationStrategy)
        {
            //todo: can you do relative and static strategies both at the same time?
            return services.AddLasso(configureOptionsFactory)
                .WithCustomKeyBuilder(keyBuilder)
                .WithCustomRelativeExpirationStrategy(relativeExpirationStrategy);
        }

        public static LassoServiceBuilder AddLasso(this IServiceCollection services,
            Action<LassoConfigOptions> configureOptionsFactory,
            IRedisKeyBuilder keyBuilder,
            IFixedExpirationStrategy fixedExpirationStrategy)
        {
            //todo: can you do relative and static strategies both at the same time?
            return services.AddLasso(configureOptionsFactory)
                .WithCustomKeyBuilder(keyBuilder)
                .WithCustomFixedExpirationStrategy(fixedExpirationStrategy);
        }

        public static LassoServiceBuilder AddDefaultLasso(this IServiceCollection services,
            Action<LassoConfigOptions> configureOptionsFactory)
        {
            //todo: can you do relative and static strategies both at the same time?
            return services.AddLasso(configureOptionsFactory)
                .WithHourlyUtcKeyBuilder()
                .WithCustomRelativeExpirationStrategy(new TimeSpanExpirationStrategy(TimeSpan.FromMinutes(10), true));
        }

        public static LassoServiceBuilder AddDefaultLasso(this IServiceCollection services)
        {
            //todo: can you do relative and static strategies both at the same time?
            return services.AddDefaultLasso(options =>
                {
                    options.RedisConfiguration = "localhost:6379";
                })
                .WithHourlyUtcKeyBuilder()
                .WithCustomRelativeExpirationStrategy(new TimeSpanExpirationStrategy(TimeSpan.FromMinutes(10), true));
        }
    }
}
