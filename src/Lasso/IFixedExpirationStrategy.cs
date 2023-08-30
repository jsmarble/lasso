using System;

namespace Lasso
{
    public interface IFixedExpirationStrategy
    {
        DateTime Expiration { get; }
    }
}
