using System;
using System.Threading.Tasks;

namespace Lasso
{
    public interface IUsageManager
    {
        Task<UsageResult> DecrementAsync(UsageRequest req, long decrement = 1, CancellationToken token = default(CancellationToken));
        Task<UsageResult> GetAsync(UsageRequest req, CancellationToken token = default(CancellationToken));
        Task<DateTime?> GetExpirationAsync(UsageRequest req, CancellationToken token = default(CancellationToken));
        Task<UsageResult> IncrementAsync(UsageRequest req, long increment = 1, CancellationToken token = default(CancellationToken));
        Task<UsageResult> ResetAsync(UsageRequest req, long init = 0, CancellationToken token = default(CancellationToken));
    }
}
