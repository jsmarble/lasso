using System;

namespace Lasso
{
    public class DateTimeExpirationStrategy : IFixedExpirationStrategy
    {
        public DateTimeExpirationStrategy(DateTime expiration)
        {
            this.Expiration = expiration;
        }

        public DateTime Expiration { get; }
    }
}
