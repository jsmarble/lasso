using System;
using System.Threading.Tasks;

namespace Lasso
{
    public interface IUsageManager
    {
        Task<UsageResult> DecrementAsync(UsageRequest req, long decrement = 1);
        Task<UsageResult> GetAsync(UsageRequest req);
        Task<DateTime?> GetExpirationAsync(UsageRequest req);
        Task<UsageResult> IncrementAsync(UsageRequest req, long increment = 1);
        Task<UsageResult> ResetAsync(UsageRequest req, long init = 0);
    }
}
