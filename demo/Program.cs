// See https://aka.ms/new-console-template for more information

using Lasso;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

const long MAX_REQ = 5;
const int LIMIT_WINDOW_SEC = 10;

Console.WriteLine("Hello, this example demonstrates usage of Lasso for managing usage limits!");
Console.WriteLine("Press [Enter] to request a new GUID be generated and written to the console.");
Console.WriteLine($"The maximum number of requests is {MAX_REQ} per {LIMIT_WINDOW_SEC} seconds.");
Console.WriteLine();

using var muxer = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
IRedisKeyBuilder keyBuilder = new DailyUtcRedisKeyBuilder();
IRelativeExpirationStrategy expirationStrategy = new TimeSpanExpirationStrategy(TimeSpan.FromSeconds(LIMIT_WINDOW_SEC), false);

var options = Options.Create(new LassoOptions { ConnectionMultiplexer = muxer });
using var usageManager = new RedisUsageManager(options, keyBuilder, expirationStrategy);
var usage = new UsageRequest("GuidGen", "LassoDemo", MAX_REQ);

while (true)
{
    Console.ReadLine();
    UsageResult res = await usageManager.IncrementAsync(usage);
    var exp = await usageManager.GetExpirationAsync(usage);
    string resetsIn = exp.HasValue
        ? $"{exp.Value.Subtract(DateTime.UtcNow).TotalSeconds:F0} seconds"
        : "never";
    Console.WriteLine($"[Usage: {res.Current} / {res.Quota}, Resets in {resetsIn}]");

    if (res.Current <= res.Quota)
        Console.WriteLine(Guid.NewGuid().ToString());
    else
        Console.WriteLine("You have exceeded the request limit. Please wait and try again.");
}
