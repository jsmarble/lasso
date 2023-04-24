namespace Lasso
{
    public class TimeSpanExpirationStrategy : IRelativeExpirationStrategy
    {
        public TimeSpanExpirationStrategy(TimeSpan expiration, bool sliding)
        {
            Expiration = expiration;
            Sliding = sliding;
        }

        public TimeSpan Expiration { get; }

        public bool Sliding { get; }
    }
}
