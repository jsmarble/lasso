using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lasso.Extensions.DependencyInjection
{
    public static class LassoServiceDependencyInjectionExtensions
    {
        private const string MissingConnectionConfigurationMessage = "A Redis connection must be configured with ConnectionMultiplexer, RedisConfigurationOptions, or RedisConfiguration.";

        /// <summary>
        /// Registers services required by Lasso.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns>A <see cref="LassoServiceBuilder"/> that can be used to further configure Lasso.</returns>
        public static LassoServiceBuilder AddLasso(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddOptions<LassoOptions>()
                .Validate(HasConnectionConfiguration, MissingConnectionConfigurationMessage)
                .ValidateOnStart();
            services.TryAddSingleton<IUsageManager>(CreateUsageManager);

            return new LassoServiceBuilder(services);
        }
        public static LassoServiceBuilder AddLasso(this IServiceCollection services, Action<LassoOptions> configureOptionsFactory)
        {
            if (configureOptionsFactory == null)
            {
                throw new ArgumentNullException(nameof(configureOptionsFactory));
            }

            var builder = services.AddLasso();
            services.Configure(configureOptionsFactory);
            return builder;
        }

        public static LassoServiceBuilder AddLasso(this IServiceCollection services,
            Action<LassoOptions> configureOptionsFactory,
            IRedisKeyBuilder keyBuilder,
            IRelativeExpirationStrategy relativeExpirationStrategy)
        {
            return services.AddLasso(configureOptionsFactory)
                .WithCustomKeyBuilder(keyBuilder)
                .WithCustomRelativeExpirationStrategy(relativeExpirationStrategy);
        }

        public static LassoServiceBuilder AddLasso(this IServiceCollection services,
            Action<LassoOptions> configureOptionsFactory,
            IRedisKeyBuilder keyBuilder,
            IFixedExpirationStrategy fixedExpirationStrategy)
        {
            return services.AddLasso(configureOptionsFactory)
                .WithCustomKeyBuilder(keyBuilder)
                .WithCustomFixedExpirationStrategy(fixedExpirationStrategy);
        }

        public static LassoServiceBuilder AddDefaultLasso(this IServiceCollection services,
            Action<LassoOptions> configureOptionsFactory)
        {
            return services.AddLasso(configureOptionsFactory)
                .WithHourlyUtcKeyBuilder()
                .WithCustomRelativeExpirationStrategy(new TimeSpanExpirationStrategy(TimeSpan.FromMinutes(10), true));
        }

        public static LassoServiceBuilder AddDefaultLasso(this IServiceCollection services)
        {
            return services.AddDefaultLasso(options =>
            {
                options.RedisConfiguration = "localhost:6379";
            });
        }

        private static IUsageManager CreateUsageManager(IServiceProvider services)
        {
            var options = services.GetRequiredService<IOptions<LassoOptions>>();
            var keyBuilder = services.GetRequiredService<IRedisKeyBuilder>();
            var relativeExpirationStrategy = services.GetService<IRelativeExpirationStrategy>();
            var fixedExpirationStrategy = services.GetService<IFixedExpirationStrategy>();
            var logger = services.GetService<ILogger<RedisUsageManager>>();

            if (relativeExpirationStrategy != null && fixedExpirationStrategy != null)
                throw new InvalidOperationException("Only one expiration strategy can be registered.");
            if (relativeExpirationStrategy != null)
                return new RedisUsageManager(options, keyBuilder, relativeExpirationStrategy, logger);
            if (fixedExpirationStrategy != null)
                return new RedisUsageManager(options, keyBuilder, fixedExpirationStrategy, logger);

            throw new InvalidOperationException("An expiration strategy must be registered.");
        }

        private static bool HasConnectionConfiguration(LassoOptions options)
        {
            return options.ConnectionMultiplexer != null
                || options.RedisConfigurationOptions != null
                || !string.IsNullOrWhiteSpace(options.RedisConfiguration);
        }
    }
}
