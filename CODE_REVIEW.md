# Lasso — Validated Code Review

Date: 2026-08-11  
Reviewed commit: `e53a49f`  
Scope: `src/Lasso`, `src/Lasso.Extensions.DependencyInjection`, `src/Lasso.Tests`, `demo`, `README.md`, and `.circleci/config.yml`

## Validation performed

This revision re-checked every assertion in the original review against source, builds, runtime probes, and upstream Redis/StackExchange.Redis behavior.

Observed evidence:

- `dotnet build src/Lasso/Lasso.csproj`: succeeds with one warning, `CS0169` for the unused `RedisUsageManager.connection` field.
- `dotnet build src/Lasso.Extensions.DependencyInjection/Lasso.Extensions.DependencyInjection.csproj`: succeeds.
- `dotnet build demo/LassoDemo.csproj`: fails with five compiler errors: wrong `RedisUsageManager` constructor, missing `UsageRequest` constructor arguments, and three assignments to read-only properties. It also warns that `exp.Value` may be null.
- DI runtime probes reproduced:
  - parameterless `AddDefaultLasso()` throws during registration;
  - `AddLasso()` plus key/expiration builders fails to resolve `IUsageManager` because `IOptions<LassoOptions>` was not registered;
  - `AddLasso(configureOptions)` without a key builder/expiration strategy fails to resolve `IUsageManager`;
  - registering both fixed and relative strategies makes the two `RedisUsageManager` constructors ambiguous;
  - default `AddLogging()` does not register non-generic `ILogger`, so `RedisUsageManager` receives its optional null value and constructs a no-op logger from `NullLoggerFactory`.
- Date-format probes for `2026-08-11` produced `20260811` under `en-US`, `14480228` under `ar-SA`, `14050520` under `fa-IR`, and `25690811` under `th-TH`.
- StackExchange.Redis 2.6.122 source confirms `KeyExpireTimeAsync` sends `PEXPIRETIME`, while conditional expiry sends `PEXPIRE`/`EXPIRE` with `NX` for `ExpireWhen.HasNoExpiry`.
- Redis documentation marks `PEXPIRETIME` and conditional `EXPIRE ... NX|XX|GT|LT` as Redis 7.0 additions.
- `dotnet list ... package --vulnerable --include-transitive` reported no known vulnerable packages for either library project using the currently configured JFrog NuGet source. This is not a guarantee against advisories absent from that feed.

---

## Confirmed findings

### High severity

#### H1. Failed batched flushes permanently lose the pending delta

`BatchedUsageManager.PushAsync` clears `delta` before awaiting the underlying Redis write:

```csharp
long d = Interlocked.Exchange(ref delta, 0);
return await usageManager.IncrementAsync(this.request, d);
```

If `IncrementAsync` throws before Redis applies the command, `d` is never restored and the next flush sees only increments added after the failed exchange. If Redis applied the command but the response was lost, retrying the same delta would instead double-count. The current API has no operation ID or documented at-most-once/at-least-once policy, so callers cannot recover safely from an ambiguous Redis timeout.

Location: `src/Lasso/BatchedUsageManager.cs:33-37`.

Fix: first define the delivery guarantee. For retryable, exactly-once-within-retention behavior, attach a unique flush ID and use Lua to atomically deduplicate that ID while applying the delta. A simpler catch path that blindly restores `d` changes loss into possible duplication and is not sufficient. Serialize flushes and add tests for definite pre-send failure, ambiguous timeout, and concurrent increments.

#### H2. Disposing `RedisUsageManager` disposes a caller-owned multiplexer

When `LassoOptions.ConnectionMultiplexer` is supplied, `ConnectSlowAsync` uses that external instance. `Dispose` later obtains it through `cache.Multiplexer`, calls `Close()`, and disposes it unconditionally.

Locations: `src/Lasso/RedisUsageManager.cs:177-193`, `src/Lasso/RedisUsageManager.cs:253-285`.

`ConnectionMultiplexer` is designed to be shared. A component receiving one from a caller should not take ownership unless the contract says so. Manual disposal of the manager can break unrelated Redis consumers using the same multiplexer.

Fix: track whether the manager created the multiplexer. Dispose it only when owned. Do not dispose an instance supplied through options.

#### H3. Built-in key builders depend on the process culture

The daily, hourly, and monthly builders call `DateTime.ToString` without `CultureInfo.InvariantCulture`. Custom date patterns still use the current culture's calendar, producing different key periods on hosts with different locale settings. The runtime probe demonstrated different years/months/days for `ar-SA`, `fa-IR`, and `th-TH`.

Locations:

- `src/Lasso/DailyUtcRedisKeyBuilder.cs:9`
- `src/Lasso/HourlyUtcRedisKeyBuilder.cs:9`
- `src/Lasso/MonthlyUtcRedisKeyBuilder.cs:9`

In a mixed-locale deployment, identical requests can be split across different Redis keys and quota totals.

Fix: format with `CultureInfo.InvariantCulture`. The original review's reference to non-ASCII digits was too broad; the reproduced problem is the culture-specific calendar value.

#### H4. The demo and README example do not compile against the current API

Both use a removed/nonexistent constructor:

```csharp
new RedisUsageManager(muxer, keyBuilder, expirationStrategy)
```

Both also construct `UsageRequest` with a parameterless object initializer even though it now has only `UsageRequest(string resource, string context, long quota)` and get-only properties.

Locations: `demo/Program.cs:18-23`, `README.md` usage example.

The demo build reproduced all five errors. `demo/Program.cs:31` and the README also dereference nullable `exp.Value` without checking it.

Fix the examples and build the demo in PR CI.

#### H5. Parameterless `AddDefaultLasso()` always throws

`AddDefaultLasso(configureOptions)` already registers the hourly key builder and relative expiration strategy. The parameterless overload calls it and registers both again. The duplicate guard throws immediately; the runtime probe reproduced:

```text
InvalidOperationException: A IRedisKeyBuilder provider has already been registered.
```

Location: `src/Lasso.Extensions.DependencyInjection/LassoServiceDependencyInjectionExtensions.cs:81-99`.

Fix: return `services.AddDefaultLasso(options => options.RedisConfiguration = "localhost:6379")` without chaining duplicate registrations.

### Medium severity

#### M1. Creating a Redis key and assigning its expiry are not atomic

`IncrementAsync`, `DecrementAsync`, and `ResetAsync` first mutate the hash, then issue expiry as a separate command. If the process dies after creating a new hash key but before expiry succeeds, that new key remains persistent.

Location: `src/Lasso/RedisUsageManager.cs:67-108`.

Important correction to the original review: mutating an existing Redis hash does not clear an existing TTL. The leak window applies when the operation creates a new key with no prior expiry; it is not a risk on every update.

Fix: use one Lua script for mutation plus conditional expiry. This also reduces the hot path from two network round trips to one. `IBatch` pipelines but does not make the pair atomic.

#### M2. The effective Redis 7.0+ requirement is undocumented

For non-sliding relative expiration, `ExpireWhen.HasNoExpiry` is encoded as `PEXPIRE/EXPIRE ... NX`; the conditional options were added in Redis 7.0. `GetExpirationAsync` uses `PEXPIRETIME`, also added in Redis 7.0.

Locations: `src/Lasso/RedisUsageManager.cs:123-151`; StackExchange.Redis 2.6.122 `RedisDatabase.cs` expiry implementation.

Scope correction: not every operation/configuration requires Redis 7. Sliding expiry with `ExpireWhen.Always` and fixed expiry use older commands. However, non-sliding relative expiry and `GetExpirationAsync` fail on Redis 6.x.

Fix: document Redis 7.0 as the minimum, or implement a compatibility path. A compatibility path for `NX` must preserve atomicity; a separate TTL check followed by EXPIRE is racy.

References:

- <https://redis.io/docs/latest/commands/expire/>
- <https://redis.io/docs/latest/commands/pexpiretime/>

#### M3. The DI registration API exposes several configurations that fail only at resolution

Confirmed cases:

1. `AddLasso()` registers `IUsageManager` but not `IOptions<LassoOptions>`. Even after chaining a key builder and expiration strategy, resolution fails unless options were registered independently.
2. `AddLasso(configureOptions)` registers options, but resolution still fails if the caller does not chain a key builder and exactly one expiration strategy.
3. Registering both fixed and relative expiration strategies is allowed by the builders, then causes an ambiguous-constructor exception when `IUsageManager` is resolved.

Locations: `src/Lasso.Extensions.DependencyInjection/LassoServiceDependencyInjectionExtensions.cs:16-87`, and the two public constructors in `src/Lasso/RedisUsageManager.cs:31-49`.

Fix:

- make `AddLasso()` call `AddOptions<LassoOptions>()`;
- validate connection configuration and required strategy registrations at startup;
- replace the two ambiguous constructors with one unambiguous dependency, preferably a single strategy contract/discriminated options value.

A unified expiration abstraction may be useful, but the original review's proposed `TimeSpan?` interface was incomplete because it could not represent absolute expiration.

#### M4. `BatchedUsageManager` remains usable after disposal and is not safe around concurrent disposal

`disposedValue` is only consulted by `Dispose`. `Increment`, `Decrement`, and `PushAsync` do not check it. Calls made after disposal continue accumulating deltas that will never be automatically flushed.

A concurrent increment can also race with the unsynchronized `if (delta != 0)` check in `Dispose` and remain pending after disposal.

Locations: `src/Lasso/BatchedUsageManager.cs:21-60`.

Fix: define lifecycle semantics, reject calls after disposal, and coordinate increment/flush/dispose state. The class claims thread safety in the README, so disposal races are part of the public reliability contract unless explicitly excluded.

#### M5. Synchronous disposal blocks on asynchronous I/O

`Dispose` calls `PushAsync().Wait()`. An arbitrary `IUsageManager` implementation may capture a synchronization context, causing deadlock; failures are also wrapped in `AggregateException`.

Location: `src/Lasso/BatchedUsageManager.cs:39-53`.

Fix: expose an explicit `FlushAsync(CancellationToken)` and an async-disposal path where target-framework support permits it. Do not make synchronous `Dispose` silently discard pending data; the replacement contract must state whether sync disposal blocks, refuses pending work, or delegates to an async lifecycle.

#### M6. Configured DI logging is ignored, and the logger field is dead

`RedisUsageManager` asks for non-generic `ILogger`. Default `AddLogging()` registers `ILogger<T>`, not non-generic `ILogger`, so DI supplies the constructor's optional null value. The constructor then creates a no-op logger from `NullLoggerFactory`. The stored logger is never used; the only warning path uses `Trace.TraceWarning`.

Locations: `src/Lasso/RedisUsageManager.cs:23-50`, `src/Lasso/RedisUsageManager.cs:134-151`.

Fix: either remove logging from the class, or inject and use `ILogger<RedisUsageManager>`. The original statement that "all log output disappears" overstated the current effect: there are no `logger` calls to disappear; the real problem is a nonfunctional logging abstraction.

#### M7. Options fail late and opaquely

If neither `ConnectionMultiplexer`, `RedisConfigurationOptions`, nor `RedisConfiguration` is set, `ConnectSlowAsync` passes null into StackExchange.Redis. Invalid configuration is discovered on first usage rather than at application startup.

Location: `src/Lasso/RedisUsageManager.cs:177-193`.

Fix: add `IValidateOptions<LassoOptions>` in the DI package and a constructor guard for direct consumers. Require at least one usable connection source and validate the highest-precedence source when multiple are supplied.

#### M8. CI does not compile all shipped projects

The PR build job compiles only `src/Lasso`; it does not compile the DI package or demo. The deploy job updates version files in the DI directory but only packs the core project. This allowed the broken demo/README contract to persist.

Location: `.circleci/config.yml`.

Fix: build a root solution containing core, DI, tests, and demo. Keep packaging as a separate job.

#### M9. CI and tests target EOL .NET 6

`.circleci/config.yml` uses `mcr.microsoft.com/dotnet/sdk:6.0-alpine`, and `Lasso.Tests.csproj` targets `net6.0`. .NET 6 has been out of support since 2024-11-12.

Fix: move CI/tests to a supported LTS SDK while retaining `netstandard2.0` for the library if that compatibility target is still required.

### Low severity / maintainability

#### L1. Null guards omit parameter names

`ArgumentNullThrowHelper.ThrowIfNull` defaults `paramName` to null, and callers never pass it. Consumers receive `ArgumentNullException` without the failing parameter name.

Location: `src/Lasso/ThrowHelpers.cs` and its call sites.

Fix: pass `nameof(...)` explicitly or use a `CallerArgumentExpression` compatibility attribute.

#### L2. `SetExpirationAsync` repeats connection lookup

Each caller has already awaited `ConnectAsync`, then `SetExpirationAsync` does it again. More importantly, callers ignore the returned `IDatabase` and read the shared field.

Locations: `src/Lasso/RedisUsageManager.cs:53-151`.

Fix: capture `IDatabase database = await ConnectAsync(...)` once and pass it into expiration handling. The original review described this as a general null-race. That was overstated: with `OnRedisError` unreachable, the practical race is concurrent disposal, which is normally outside an `IDisposable` object's supported lifecycle. Treat this as cleanup/performance, not a high-severity concurrency bug.

#### L3. Fixed-expiration `DateTimeKind.Unspecified` fails only on first Redis operation

StackExchange.Redis 2.6.122 converts `Local` to UTC and accepts `Utc`, but throws for `DateTimeKind.Unspecified`. `DateTimeExpirationStrategy` accepts any `DateTime`, delaying this error until expiry is applied.

Location: `src/Lasso/DateTimeExpirationStrategy.cs`.

Fix: validate the kind in the strategy constructor. Correction: the original review incorrectly said local values silently shift; they are deliberately converted to UTC.

#### L4. `DeleteAsync` always issues an expiry operation after deletion

If the deleted field was the last hash field, Redis removes the key and the subsequent expiry command is a no-op. If other fields remain and expiration is sliding, deletion renews their shared key TTL. The latter may be intended, but it should be explicit.

Location: `src/Lasso/RedisUsageManager.cs:109-121`.

Do not add a standalone `HLEN` merely to avoid this call; that adds another round trip and race. If mutation/expiry is moved to Lua, return enough state from the script to handle it atomically.

#### L5. Cancellation behavior is inconsistent and limited

The public token cancels before work and while waiting for `connectionLock`; StackExchange.Redis 2.6.122 operations themselves do not accept it. `GetAsync`, `ResetAsync`, and `DeleteAsync` also omit the token when calling `SetExpirationAsync`, unlike increment/decrement.

Fix: pass the token consistently and document that Redis command timeout is controlled by StackExchange.Redis configuration, not by the method token.

#### L6. Tests mix integration, timing, and unit concerns

- Most `RedisUsageManagerTests` require a live Redis server at `127.0.0.1:6379`.
- `One_Thousand_Increments_Perf_Test` asserts completion under two seconds, a machine/load-dependent threshold.
- Many tests block with `.Result` instead of using `async Task`.
- There are no DI tests for the failure cases reproduced above.
- There are no batched-write failure/disposal-race tests.

Fix: label/split Redis integration tests, move throughput measurement to BenchmarkDotNet or remove the wall-clock assertion, use async tests, and add contract tests for DI registration and failed batch flushes.

#### L7. Dead code and consistency issues

Confirmed cleanup:

- unused `RedisUsageManager.connection` field (`CS0169`);
- unreachable `OnRedisError` and its large commented-out reconnect implementation;
- unreachable "no expiration strategy" branch because both public constructors require and validate a strategy;
- unused `logger` field in practice;
- large commented-out DI connection code;
- duplicate `<ImplicitUsings>` entries in both library project files;
- unused imports such as `System.Data.Common`;
- mixed `default` and `default(CancellationToken)` syntax;
- XML typo `Lass0`.

#### L8. Counter overflow semantics are inconsistent

`Interlocked.Add` wraps a `long` delta on overflow, while Redis `HINCRBY` reports an error when its signed 64-bit result would overflow. A sufficiently large accumulated batch can therefore send a corrupted wrapped delta rather than failing.

Location: `src/Lasso/BatchedUsageManager.cs:21-36`.

This is an edge case, but a usage-accounting library should choose explicit checked/unchecked semantics and test them.

---

## Performance findings

### P1. Mutating operations use two sequential Redis round trips

The hash command is awaited before the expiry command is sent. Network RTT dominates the local allocations and branches in these methods.

Preferred fix: one Lua script that returns the updated counter and applies expiry atomically. This addresses both performance and M1.

Do not use `CommandFlags.FireAndForget` for expiry as the original review suggested. Losing an expiry command creates persistent keys and weakens data reliability.

### P2. `GetAsync` is a write for sliding expiration

`GetAsync` reads the hash and then refreshes the key expiry when sliding expiration is configured. This is consistent with sliding-expiration semantics, but callers should know that reads create master write load and cost a second RTT.

This is an operational behavior to document, not necessarily a defect or a reason to change the API.

### P3. Local key-format allocations are not currently worth optimizing

The original review suggested caching daily/monthly formatted prefixes. That is premature relative to the Redis network cost and introduces rollover/concurrency complexity. Use invariant formatting for correctness; optimize allocation only after benchmarking shows it matters.

---

## Security assessment

No direct injection, credential exposure, or known dependency vulnerability was found in the reviewed code:

- Redis keys and hash fields are passed as StackExchange.Redis values, not concatenated into raw Redis commands.
- `ConfigurationOptions` already supports authentication and TLS; the original claim that Lasso lacked first-class TLS/auth support was incorrect.
- No configured connection string or password is logged.
- The package vulnerability scan was clean against the configured NuGet feed.

Remaining operational responsibilities should be documented rather than implemented as library policy: keep credentials outside source, enable TLS for remote Redis, restrict Redis network access, and constrain user-controlled key cardinality at the application boundary.

The original concern about NuGet and SSH credentials existing in one CI job was speculative and not a code-review finding; it has been removed.

---

## Findings rejected or downgraded from the original review

1. **"No reconnect path leaves a broken `IDatabase` forever" — rejected.** StackExchange.Redis automatically reconnects in the background and explicitly recommends sharing/reusing a `ConnectionMultiplexer`. `IDatabase` is a lightweight proxy over it. `OnRedisError` is dead code and should be deleted, not wired up as a custom reconnect loop. Replacing the multiplexer on every connection exception would fight the client's intended behavior.
2. **"A colon in `Context` causes key collisions" — rejected.** For a fixed timestamp prefix, `prefix + ":" + context` is injective over distinct context strings; embedded colons do not create collisions. A configurable application prefix could still be useful for shared Redis databases, but that is namespacing, not collision prevention.
3. **"Missing atomic quota enforcement is a core defect" — rejected as a defect.** The README explicitly says Lasso is a record keeper and does not prescribe outcomes when thresholds are crossed. `TryIncrementWithinQuotaAsync` could be a useful optional product feature, but it changes the stated scope.
4. **"BatchedUsageManager should implement `IUsageManager`" — rejected.** Its buffered synchronous increment/flush contract is materially different from the full read/reset/delete interface. If consumers need DI substitution, define a separate batching interface; forcing it into `IUsageManager` would be a leaky abstraction.
5. **"The no-strategy warning logs on every operation" — rejected.** The branch is unreachable through public constructors because each requires and null-validates one strategy. Delete the dead branch.
6. **"Non-sliding expiration semantics are surprising and undocumented" — rejected.** The README states that a 24-hour relative expiration begins when usage starts. The first-write anchored behavior is already described.
7. **"`DateTime.MaxValue`/`TimeSpan.MaxValue` are undocumented magic" — rejected.** The README documents both, and StackExchange.Redis itself treats null/`MaxValue` expiry as `PERSIST` in this API version.
8. **"Skip zero-delta pushes" — withdrawn.** `PushAsync` returns a current `UsageResult`; skipping the call would require cached state or a different return contract. The zero write may be wasteful, but the prior fix was incomplete.
9. **"Plain `disposedValue` in RedisUsageManager is a material memory-model bug" — downgraded.** Concurrent use during disposal is not generally guaranteed for `IDisposable`. Reusing the returned database is cleaner, but this is not a priority unless concurrent disposal becomes an explicit contract.
10. **"Cache formatted key prefixes" — rejected absent benchmarks.** Redis RTT dominates; correctness comes first.
11. **"Use fire-and-forget expiry" — rejected.** It conflicts with reliable key expiry.
12. **"Local `DateTime` silently shifts expiry" — rejected.** StackExchange.Redis converts `Local` to UTC and rejects `Unspecified`.

---

## Recommended order of work

| Priority | Change | Why |
|---|---|---|
| 1 | Define batch delivery semantics and make flushes idempotent/recoverable | Prevents silent loss without introducing duplicate usage |
| 2 | Stop disposing caller-owned multiplexers | Restores correct resource ownership |
| 3 | Use invariant key formatting | Prevents cross-host quota splitting |
| 4 | Fix DI registrations and constructor ambiguity; add DI tests | Multiple public setup paths currently throw |
| 5 | Fix and CI-build demo/README example | Public contract currently does not compile |
| 6 | Make mutation plus first expiry atomic with Lua | Prevents persistent orphan keys and halves RTTs |
| 7 | Document or handle Redis 7 compatibility | Prevents runtime failures on Redis 6 |
| 8 | Replace blocking batch disposal with an explicit lifecycle | Avoids deadlock and ambiguous failure handling |
| 9 | Validate options and fixed `DateTime` values at startup | Converts late failures into actionable startup errors |
| 10 | Upgrade test/CI runtime and split integration/perf tests | Removes EOL runtime and flaky coverage |
