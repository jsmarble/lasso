[![CircleCI](https://dl.circleci.com/status-badge/img/gh/jsmarble/lasso/tree/main.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/gh/jsmarble/lasso/tree/main)
![Nuget](https://img.shields.io/nuget/dt/Lasso?logo=NuGet&labelColor=%23004880&color=gray)

# Lasso

A usage quota wrangler.

Lasso helps keep track of any kind of quotas or metrics by incrementing or decrementing usage. It stores usage metrics, separated by a specified context, as numbers in a Redis Hashset for named resources. This keeps all usage metrics for a particular context together.

Lasso manages the scope of the usage data through customizable Redis key builders. These typically will be time-based, such as daily or monthly, but are completely customizable for any need.

Lasso does not modify the behavior of your application or prescribe any outcomes for crossing usage thresholds. Lasso is nothing more than a lightweight record keeper.

Lasso is designed to be asynchronous, thread safe, and fast. Calls to the UsageManager class are asynchronous and directly result in calls to Redis. Calls to the BatchedUsageManager class are thread safe.

## Technicals

Lasso uses Redis to store a HashSet of resources and usage counters. This HashSet is keyed in Redis based on a strategy chosen by the consuming application. There are a few built-in strategies, such as Daily or Monthly that take an identifier and build a composite key (e.g. `20220424:tenantA`). The timestamp is not strictly required, but is useful if historical data is to be kept around for some period.

When storing the HashSet, Lasso also sets an expiration on the key according to a strategy chosen by the consuming application. There are a few built-in strategies, such as relative or fixed times that use `TimeSpan` and `DateTime`, respectively. Providing `TimeSpan.MaxValue` or `DateTime.MaxValue` will result in no expiration and the usage HashSet keys being retained for the life of the server. The relative expiration also allows for sliding windows.

A combination of these two strategies enables a very flexible approach to how usage data is stored and how long it is retained. Custom implementations of either key or expiration strategies can be written by the consuming application.

### Examples

1. The key strategy only contains a tenant `GUID` and an expiration strategy of 24 hours. Once the usage begins for that tenant, all data will be reset in 24 hours.
1. The key strategy uses a daily timestamp and an expiration strategy of 30 days. Each day, the key will change for that day's usage, but each day's usage will be retained for 30 days.

## Usage

Using Lasso only requires a few things.

1. A Redis server
1. A strategy for building a Redis key to represent the context of your usage (e.g. a customer or tenant identifier plus an optional timestamp)
1. A strategy for specifying the expiration date of the Redis key that stores resource usage.

This example builds a composite key for `tenant_abc` per day, as in `20220318:tenant_abc` and expires it after 30 days, thereby keeping daily usage for the trailing 30 days. It increments usage for resource `background_service` and passes a quota value of `100` along with the request/response. It then gets the expiration for the key and displays the results in the console.

```csharp
var redis = ConnectionMultiplexer.Connect("10.0.2.12:6379");
IRedisKeyBuilder keyBuilder = new DailyUtcRedisKeyBuilder();
IRelativeExpirationStrategy expirationStrategy = new TimeSpanExpirationStrategy(TimeSpan.FromDays(30), sliding: false);
IUsageManager usageManager = new RedisUsageManager(redis, keyBuilder, expirationStrategy);

var usage = new UsageRequest
{
    Context = "tenant_abc",
    Resource = "background_service",
    Quota = 100
};

UsageResult res = await usageManager.IncrementAsync(usage);
var exp = await usageManager.GetExpirationAsync(usage);
Console.WriteLine($"Usage: {res.Current} / {res.Quota}, Resets in {exp.Value.Subtract(DateTime.UtcNow).TotalDays} days");
```
