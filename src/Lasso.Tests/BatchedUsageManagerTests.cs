using Moq;
using StackExchange.Redis;
using System.Diagnostics;

namespace Lasso.Tests
{
    public class BatchedUsageManagerTests
    {
        [Test]
        public async Task Increment_Calls_Batched()
        {
            UsageRequest req = new UsageRequest
            {
                Context = Guid.NewGuid().ToString(),
                Quota = 100,
                Resource = "password_resets"
            };
            int increments = 10;


            var usageManagerMock = new Mock<IUsageManager>();
            usageManagerMock.Setup(x => x.IncrementAsync(req, increments, default(CancellationToken)));

            using (BatchedUsageManager bum = new BatchedUsageManager(usageManagerMock.Object, req))
            {
                for (int i = 0; i < increments; i++)
                {
                    bum.Increment();
                }
                await bum.PushAsync();
            }

            usageManagerMock.VerifyAll();
        }

        [Test]
        public async Task Decrement_Calls_Batched()
        {
            UsageRequest req = new UsageRequest
            {
                Context = Guid.NewGuid().ToString(),
                Quota = 100,
                Resource = "password_resets"
            };
            int decrements = 10;

            var usageManagerMock = new Mock<IUsageManager>();
            usageManagerMock.Setup(x => x.IncrementAsync(req, -decrements, default(CancellationToken)));

            using (BatchedUsageManager bum = new BatchedUsageManager(usageManagerMock.Object, req))
            {
                for (int i = 0; i < decrements; i++)
                {
                    bum.Decrement();
                }
                await bum.PushAsync();
            }

            usageManagerMock.VerifyAll();
        }

        [Test]
        public async Task Increment_Decrement_Calls_Batched()
        {
            UsageRequest req = new UsageRequest
            {
                Context = Guid.NewGuid().ToString(),
                Quota = 100,
                Resource = "password_resets"
            };
            int increments = 15;
            int decrements = 10;

            var usageManagerMock = new Mock<IUsageManager>();
            usageManagerMock.Setup(x => x.IncrementAsync(req, increments - decrements, default(CancellationToken)));

            using (BatchedUsageManager bum = new BatchedUsageManager(usageManagerMock.Object, req))
            {
                for (int i = 0; i < increments; i++)
                {
                    bum.Increment();
                }
                for (int i = 0; i < decrements; i++)
                {
                    bum.Decrement();
                }
                await bum.PushAsync();
            }

            usageManagerMock.VerifyAll();
        }

        [Test]
        public async Task Multiple_Push_Calls_Uses_Relative_Deltas()
        {
            UsageRequest req = new UsageRequest
            {
                Context = Guid.NewGuid().ToString(),
                Quota = 100,
                Resource = "password_resets"
            };
            int increments = 15;
            int decrements = 10;

            var usageManagerMock = new Mock<IUsageManager>();
            usageManagerMock.Setup(x => x.IncrementAsync(req, increments, default(CancellationToken)));
            usageManagerMock.Setup(x => x.IncrementAsync(req, -decrements, default(CancellationToken)));

            using (BatchedUsageManager bum = new BatchedUsageManager(usageManagerMock.Object, req))
            {
                for (int i = 0; i < increments; i++)
                {
                    bum.Increment();
                }
                await bum.PushAsync();
                for (int i = 0; i < decrements; i++)
                {
                    bum.Decrement();
                }
                //DO NOT CALL bum.PushAsync();
            }

            usageManagerMock.VerifyAll();
        }

        [Test]
        public void Dispose_With_Pending_Calls_Push()
        {
            UsageRequest req = new UsageRequest
            {
                Context = Guid.NewGuid().ToString(),
                Quota = 100,
                Resource = "password_resets"
            };
            int increments = 10;

            var usageManagerMock = new Mock<IUsageManager>();
            usageManagerMock.Setup(x => x.IncrementAsync(req, increments, default(CancellationToken)));

            using (BatchedUsageManager bum = new BatchedUsageManager(usageManagerMock.Object, req))
            {
                for (int i = 0; i < increments; i++)
                {
                    bum.Increment();
                }
                //DO NOT CALL bum.PushAsync();
            }

            usageManagerMock.VerifyAll();
        }

        [Test]
        public void Dispose_Without_Pending_Does_Not_Throw_Exception()
        {
            UsageRequest req = new UsageRequest
            {
                Context = Guid.NewGuid().ToString(),
                Quota = 100,
                Resource = "password_resets"
            };

            var usageManagerMock = new Mock<IUsageManager>();

            using (BatchedUsageManager bum = new BatchedUsageManager(usageManagerMock.Object, req))
            {
                //do nothing
            }
        }

        [Test]
        public async Task One_Million_Increment_Calls_Works_Fast()
        {
            UsageRequest req = new UsageRequest
            {
                Context = Guid.NewGuid().ToString(),
                Quota = 100,
                Resource = "password_resets"
            };
            int increments = 1000000;

            Stopwatch sw = Stopwatch.StartNew();
            var usageManagerMock = new Mock<IUsageManager>();
            usageManagerMock.Setup(x => x.IncrementAsync(req, increments, default(CancellationToken)));

            using (BatchedUsageManager bum = new BatchedUsageManager(usageManagerMock.Object, req))
            {
                for (int i = 0; i < increments; i++)
                {
                    bum.Increment();
                }
                await bum.PushAsync();
            }

            sw.Stop();
            usageManagerMock.VerifyAll();
            TimeSpan max_acceptable_time = TimeSpan.FromSeconds(1);
            Assert.That(sw.Elapsed, Is.Not.EqualTo(TimeSpan.Zero));
            Assert.That(sw.Elapsed, Is.LessThan(max_acceptable_time));
        }
    }
}
