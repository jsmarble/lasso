namespace Lasso
{
    public class BatchedUsageManager : IDisposable
    {
        private bool disposedValue;
        private long delta;

        private readonly IUsageManager usageManager;
        private readonly UsageRequest request;

        public BatchedUsageManager(IUsageManager usageManager, UsageRequest request)
        {
            this.usageManager = usageManager;
            this.request = request;
        }

        public long Pending => delta;

        public void Increment(long increment = 1)
        {
            Interlocked.Add(ref delta, increment);
        }

        public void Decrement(long decrement = 1)
        {
            Interlocked.Add(ref delta, -decrement);
        }

        public async Task<UsageResult> PushAsync()
        {
            long d = Interlocked.Exchange(ref delta, 0);
            return await usageManager.IncrementAsync(this.request, d);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    if (delta != 0)
                        PushAsync().Wait();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
