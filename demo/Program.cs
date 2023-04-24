// See https://aka.ms/new-console-template for more information

using Lasso;
using StackExchange.Redis;

const long MAX_REQ = 5;
const int LIMIT_WINDOW_SEC = 10;

Console.WriteLine("Hello, this example demonstrates usage of Lasso for managing usage limits!");
Console.WriteLine("Press [Enter] to request a new GUID be generated and written to the console.");
Console.WriteLine($"The maximum number of requests is {MAX_REQ} per {LIMIT_WINDOW_SEC} seconds.");
Console.WriteLine();

var muxer = ConnectionMultiplexer.Connect("10.0.2.12:6379");
IRedisKeyBuilder keyBuilder = new DailyUtcRedisKeyBuilder();
IRelativeExpirationStrategy expirationStrategy = new TimeSpanExpirationStrategy(TimeSpan.FromSeconds(LIMIT_WINDOW_SEC), false);

IUsageManager usageManager = new RedisUsageManager(muxer, keyBuilder, expirationStrategy);
var usage = new UsageRequest
{
    Context = "LassoDemo",
    Resource = "GuidGen",
    Quota = MAX_REQ
};

while (true)
{
    Console.ReadLine();
    UsageResult res = await usageManager.IncrementAsync(usage);
    var exp = await usageManager.GetExpirationAsync(usage);
    Console.WriteLine($"[Usage: {res.Current} / {res.Quota}, Resets in {exp.Value.Subtract(DateTime.UtcNow).TotalSeconds} seconds]");

    if (res.Current <= res.Quota)
        Console.WriteLine(Guid.NewGuid().ToString());
    else
        Console.WriteLine("You have exceeded the request limit. Please wait and try again.");
}
