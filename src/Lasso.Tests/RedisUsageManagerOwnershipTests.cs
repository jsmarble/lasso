using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Lasso.Tests
{
    public class RedisUsageManagerOwnershipTests
    {
        [Test]
        public async Task Dispose_DoesNotDisposeSuppliedConnectionMultiplexer()
        {
            var database = new Mock<IDatabase>();
            database
                .Setup(x => x.KeyExpireTimeAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync((DateTime?)null);

            var connection = new Mock<IConnectionMultiplexer>();
            connection
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(database.Object);

            var options = Options.Create(new LassoOptions { ConnectionMultiplexer = connection.Object });
            var manager = new RedisUsageManager(
                options,
                new DailyUtcRedisKeyBuilder(),
                new TimeSpanExpirationStrategy(TimeSpan.FromMinutes(1), sliding: false));

            await manager.GetExpirationAsync(new UsageRequest("resource", "context", 10));
            manager.Dispose();

            connection.Verify(x => x.Close(It.IsAny<bool>()), Times.Never);
            connection.Verify(x => x.Dispose(), Times.Never);
        }

        [Test]
        public void Constructor_RejectsMissingConnectionConfiguration()
        {
            var options = Options.Create(new LassoOptions());

            OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
                new RedisUsageManager(
                    options,
                    new DailyUtcRedisKeyBuilder(),
                    new TimeSpanExpirationStrategy(TimeSpan.FromMinutes(1), sliding: false)));

            Assert.That(exception.Message, Does.Contain("A Redis connection must be configured"));
        }
    }
}
