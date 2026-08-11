using Lasso.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Lasso.Tests
{
    public class LassoServiceDependencyInjectionTests
    {
        [Test]
        public void AddDefaultLasso_ResolvesUsageManager()
        {
            var services = new ServiceCollection();
            services.AddDefaultLasso();

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.That(provider.GetRequiredService<IUsageManager>(), Is.TypeOf<RedisUsageManager>());
        }

        [Test]
        public void AddLasso_WithoutConnectionConfiguration_FailsWhenHostStarts()
        {
            using IHost host = new HostBuilder()
                .ConfigureServices((_, services) => services.AddLasso()
                    .WithDailyUtcKeyBuilder()
                    .WithTimeSpanExpirationStrategy(TimeSpan.FromMinutes(1), sliding: false))
                .Build();

            OptionsValidationException exception = Assert.ThrowsAsync<OptionsValidationException>(
                async () => await host.StartAsync());
            Assert.That(exception.Message, Does.Contain("A Redis connection must be configured"));
        }

        [Test]
        public void AddLasso_WithBothExpirationStrategies_RejectsResolution()
        {
            var services = new ServiceCollection();
            services.AddLasso(options => options.RedisConfiguration = "localhost:6379")
                .WithDailyUtcKeyBuilder()
                .WithTimeSpanExpirationStrategy(TimeSpan.FromMinutes(1), sliding: false)
                .WithDateTimeExpirationStrategy(DateTime.UtcNow.AddMinutes(1));

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.That(
                () => provider.GetRequiredService<IUsageManager>(),
                Throws.InvalidOperationException.With.Message.EqualTo("Only one expiration strategy can be registered."));
        }

        [Test]
        public void AddLasso_RejectsNullOptionsConfiguration()
        {
            var services = new ServiceCollection();

            Assert.That(
                () => services.AddLasso(configureOptionsFactory: null),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("configureOptionsFactory"));
        }
    }
}
