using System;

namespace Lasso
{
    public class CustomRedisKeyBuilder : IRedisKeyBuilder
    {
        private readonly Func<UsageRequest, string> func;

        public CustomRedisKeyBuilder(Func<UsageRequest, string> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            this.func = func;
        }

        public string BuildRedisKey(UsageRequest usageRequest)
        {
            return func(usageRequest);
        }
    }
}
