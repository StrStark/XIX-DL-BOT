using System.Diagnostics;
using DownloaderBot.Telegram.RateLimiter;
using Xunit;

namespace DownloaderBot.Tests;

public class RateLimiterTests
{
    [Fact]
    public async Task TokenBucket_LimitsBurst()
    {
        var b = new TokenBucketLimiter(ratePerSec: 10);
        var sw = Stopwatch.StartNew();
        // Bucket starts full (10); the 20th call must wait at least ~1s for refill.
        for (int i = 0; i < 20; i++) await b.WaitAsync(default);
        sw.Stop();
        Assert.True(sw.Elapsed.TotalMilliseconds > 800, $"Was {sw.Elapsed.TotalMilliseconds}ms");
    }
}
