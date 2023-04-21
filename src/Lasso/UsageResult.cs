namespace Lasso
{
    public class UsageResult
    {
        public string Resource { get; set; }
        public long Quota { get; set; }
        public long Current { get; set; }
        public string Context { get; set; }
    }
}
