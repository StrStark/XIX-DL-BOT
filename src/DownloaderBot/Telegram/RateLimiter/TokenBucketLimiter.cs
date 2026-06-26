using System.Collections.Concurrent;

namespace DownloaderBot.Telegram.RateLimiter;

// Simple token-bucket: refills `ratePerSec` tokens per second up to `ratePerSec` capacity.
public sealed class TokenBucketLimiter
{
    readonly int _ratePerSec;
    double _tokens;
    DateTime _lastRefill;
    readonly object _gate = new();

    public TokenBucketLimiter(int ratePerSec)
    {
        _ratePerSec = Math.Max(1, ratePerSec);
        _tokens = _ratePerSec;
        _lastRefill = DateTime.UtcNow;
    }

    public async Task WaitAsync(CancellationToken ct)
    {
        while (true)
        {
            TimeSpan wait;
            lock (_gate)
            {
                Refill();
                if (_tokens >= 1)
                {
                    _tokens -= 1;
                    return;
                }
                var deficit = 1 - _tokens;
                wait = TimeSpan.FromSeconds(deficit / _ratePerSec);
            }
            await Task.Delay(wait < TimeSpan.FromMilliseconds(10) ? TimeSpan.FromMilliseconds(10) : wait, ct);
        }
    }

    void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        if (elapsed <= 0) return;
        _tokens = Math.Min(_ratePerSec, _tokens + elapsed * _ratePerSec);
        _lastRefill = now;
    }
}

public sealed class PerChatLimiter
{
    readonly int _ratePerSec;
    readonly ConcurrentDictionary<long, TokenBucketLimiter> _buckets = new();
    public PerChatLimiter(int ratePerSec) => _ratePerSec = ratePerSec;
    public Task WaitAsync(long chatId, CancellationToken ct)
        => _buckets.GetOrAdd(chatId, _ => new TokenBucketLimiter(_ratePerSec)).WaitAsync(ct);
}
