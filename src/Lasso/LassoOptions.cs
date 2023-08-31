using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Lasso
{
    public class LassoOptions : IOptions<LassoOptions>
    {
        /// <summary>
        /// The configuration used to connect to Redis.
        /// </summary>
        public string RedisConfiguration { get; set; }

        /// <summary>
        /// The configuration used to connect to Redis.
        /// This is preferred over Configuration.
        /// </summary>
        public ConfigurationOptions RedisConfigurationOptions { get; set; }

        /// <summary>
        /// The StackExchange.Redis IConnectionMultiplexer to use for opening Redis connections.
        /// This optional paramter overrides any other configuration.
        /// </summary>
        public IConnectionMultiplexer ConnectionMultiplexer { get; set; }

        LassoOptions IOptions<LassoOptions>.Value => this;
    }
}
