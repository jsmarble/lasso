using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Lasso.Extensions.DependencyInjection;

//*** Josh - this class should be in lasso and 
public class LassoConfigOptions : IOptions<LassoConfigOptions>
{
    /// <summary>
    /// The configuration used to connect to Redis.
    /// </summary>
    public string? RedisConfiguration { get; set; }

    /// <summary>
    /// The configuration used to connect to Redis.
    /// This is preferred over Configuration.
    /// </summary>
    public ConfigurationOptions? RedisConfigurationOptions { get; set; }

    LassoConfigOptions IOptions<LassoConfigOptions>.Value => this;
}