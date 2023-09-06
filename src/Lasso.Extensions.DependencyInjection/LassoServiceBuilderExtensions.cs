using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System;

namespace Lasso.Extensions.DependencyInjection
{
    public static class LassoServiceBuilderExtensions
    {
        #region Expiration Strategies

        #region IRelativeExpirationStrategy Strategies

        public static LassoServiceBuilder WithTimeSpanExpirationStrategy(this LassoServiceBuilder builder, TimeSpan expiration, bool sliding)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (builder.Services.Any<ServiceDescriptor>((Func<ServiceDescriptor, bool>)(x => x.ServiceType == typeof(IRelativeExpirationStrategy))))
                throw new InvalidOperationException($"A {nameof(IRelativeExpirationStrategy)} provider has already been registered. Only a single strategy of this type may be registered.");

            builder.Services.TryAddSingleton<IRelativeExpirationStrategy>(s => new TimeSpanExpirationStrategy(expiration, sliding));
            return builder;
        }

        //todo: you can add more variations of TimeSpanExpirationStrategy builder methods, i.e. introduce an Option class specific to TimeSpanExpirationStrategy

        public static LassoServiceBuilder WithCustomRelativeExpirationStrategy(this LassoServiceBuilder builder, IRelativeExpirationStrategy relativeExpirationStrategy)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (relativeExpirationStrategy == null) throw new ArgumentNullException(nameof(relativeExpirationStrategy));
            if (builder.Services.Any<ServiceDescriptor>((Func<ServiceDescriptor, bool>)(x => x.ServiceType == typeof(IRelativeExpirationStrategy))))
                throw new InvalidOperationException($"A {nameof(IRelativeExpirationStrategy)} provider has already been registered. Only a single strategy of this type may be registered.");

            builder.Services.TryAddSingleton<IRelativeExpirationStrategy>(relativeExpirationStrategy);
            return builder;
        }

        public static LassoServiceBuilder WithCustomRelativeExpirationStrategy<T>(this LassoServiceBuilder builder) where T : class, IRelativeExpirationStrategy
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (builder.Services.Any<ServiceDescriptor>((Func<ServiceDescriptor, bool>)(x => x.ServiceType == typeof(IRelativeExpirationStrategy))))
                throw new InvalidOperationException($"A {nameof(IRelativeExpirationStrategy)} provider has already been registered. Only a single strategy of this type may be registered.");

            builder.Services.TryAddSingleton<IRelativeExpirationStrategy, T>();
            return builder;
        }

        #endregion

        #region IFixedExpirationStrategy Strategies

        public static LassoServiceBuilder WithDateTimeExpirationStrategy(this LassoServiceBuilder builder, DateTime expirationDate)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (builder.Services.Any<ServiceDescriptor>((Func<ServiceDescriptor, bool>)(x => x.ServiceType == typeof(IFixedExpirationStrategy))))
                throw new InvalidOperationException($"A {nameof(IFixedExpirationStrategy)} provider has already been registered. Only a single strategy of this type may be registered.");

            builder.Services.TryAddSingleton<IFixedExpirationStrategy>(s => new DateTimeExpirationStrategy(expirationDate));
            return builder;
        }

        public static LassoServiceBuilder WithCustomFixedExpirationStrategy(this LassoServiceBuilder builder, IFixedExpirationStrategy fixedExpirationStrategy)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (fixedExpirationStrategy == null) throw new ArgumentNullException(nameof(fixedExpirationStrategy));
            if (builder.Services.Any<ServiceDescriptor>((Func<ServiceDescriptor, bool>)(x => x.ServiceType == typeof(IFixedExpirationStrategy))))
                throw new InvalidOperationException($"A {nameof(IFixedExpirationStrategy)} provider has already been registered. Only a single strategy of this type may be registered.");

            builder.Services.TryAddSingleton<IFixedExpirationStrategy>(fixedExpirationStrategy);
            return builder;
        }

        public static LassoServiceBuilder WithCustomFixedExpirationStrategy<T>(this LassoServiceBuilder builder) where T : class, IFixedExpirationStrategy
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (builder.Services.Any<ServiceDescriptor>((Func<ServiceDescriptor, bool>)(x => x.ServiceType == typeof(IFixedExpirationStrategy))))
                throw new InvalidOperationException($"A {nameof(IFixedExpirationStrategy)} provider has already been registered. Only a single strategy of this type may be registered.");

            builder.Services.TryAddSingleton<IFixedExpirationStrategy, T>();
            return builder;
        }

        #endregion

        #endregion

        #region Key Builders

        public static LassoServiceBuilder WithDailyUtcKeyBuilder(this LassoServiceBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (builder.Services.Any<ServiceDescriptor>((Func<ServiceDescriptor, bool>)(x => x.ServiceType == typeof(IRedisKeyBuilder))))
                throw new InvalidOperationException($"A {nameof(IRedisKeyBuilder)} provider has already been registered. Only a single strategy of this type may be registered.");

            builder.Services.TryAddSingleton<IRedisKeyBuilder, DailyUtcRedisKeyBuilder>();
            return builder;
        }

        public static LassoServiceBuilder WithHourlyUtcKeyBuilder(this LassoServiceBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (builder.Services.Any<ServiceDescriptor>((Func<ServiceDescriptor, bool>)(x => x.ServiceType == typeof(IRedisKeyBuilder))))
                throw new InvalidOperationException($"A {nameof(IRedisKeyBuilder)} provider has already been registered. Only a single strategy of this type may be registered.");

            builder.Services.TryAddSingleton<IRedisKeyBuilder, HourlyUtcRedisKeyBuilder>();
            return builder;
        }

        //todo: add more for OOTB key builders

        public static LassoServiceBuilder WithCustomKeyBuilder(this LassoServiceBuilder builder, IRedisKeyBuilder keyBuilder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (keyBuilder == null) throw new ArgumentNullException(nameof(keyBuilder));
            if (builder.Services.Any<ServiceDescriptor>((Func<ServiceDescriptor, bool>)(x => x.ServiceType == typeof(IRedisKeyBuilder))))
                throw new InvalidOperationException($"A {nameof(IRedisKeyBuilder)} provider has already been registered. Only a single strategy of this type may be registered.");

            builder.Services.TryAddSingleton<IRedisKeyBuilder>(keyBuilder);
            return builder;
        }

        public static LassoServiceBuilder WithCustomKeyBuilder<T>(this LassoServiceBuilder builder) where T : class, IRedisKeyBuilder
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (builder.Services.Any<ServiceDescriptor>((Func<ServiceDescriptor, bool>)(x => x.ServiceType == typeof(IRedisKeyBuilder))))
                throw new InvalidOperationException($"A {nameof(IRedisKeyBuilder)} provider has already been registered. Only a single strategy of this type may be registered.");

            builder.Services.TryAddSingleton<IRedisKeyBuilder, T>();
            return builder;
        }

        #endregion
    }
}
