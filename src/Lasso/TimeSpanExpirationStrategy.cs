namespace Lasso
{
    public class TimeSpanExpirationStrategy : IRelativeExpirationStrategy
    {
        public TimeSpanExpirationStrategy(TimeSpan expiration)
        {
            Expiration = expiration;
        }

        public TimeSpan Expiration { get; }
    }
}
