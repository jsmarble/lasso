using System;

namespace Lasso
{
    public class UsageRequest
    {
        public UsageRequest(string resource, string context, long quota)
        {
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Quota = quota;
        }

        public string Resource { get; }
        public long Quota { get; }
        public string Context { get; }
    }
}
