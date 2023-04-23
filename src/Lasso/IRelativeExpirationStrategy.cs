namespace Lasso
{
    public interface IRelativeExpirationStrategy
    {
        public TimeSpan Expiration { get; }
    }
}
