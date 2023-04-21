namespace Lasso
{
    public class UsageRequest
    {
        public string Resource { get; set; }
        public long Quota { get; set; }
        public string Context { get; set; }
    }
}
