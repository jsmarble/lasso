using System;

namespace Lasso
{
    public class UsageResult
    {
        public UsageResult(string resource, string context, long quota, long current)
        {
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Quota = quota;
            Current = current;
        }

        public string Resource { get; }
        public long Quota { get; }
        public long Current { get; }
        public string Context { get; }
    }
}
