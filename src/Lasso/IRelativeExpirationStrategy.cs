using System;

namespace Lasso
{
    public interface IRelativeExpirationStrategy
    {
        TimeSpan Expiration { get; }
        bool Sliding { get; }
    }
}
