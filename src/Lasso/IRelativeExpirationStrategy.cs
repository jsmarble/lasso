namespace Lasso
{
    public interface IRelativeExpirationStrategy
    {
        public TimeSpan Expiration { get; }
        public bool Sliding { get; }
    }
}
