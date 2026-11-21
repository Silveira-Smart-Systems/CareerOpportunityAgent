using COA.Application.Discovery;

namespace COA.Infrastructure.Discovery.Jobicy;

/// <summary>Explicit, opt-in configuration for the experimental Jobicy adapter.</summary>
public sealed class JobicyAdapterOptions
{
    public const string SectionName = "Discovery:Jobicy";

    /// <summary>The adapter makes no HTTP request unless this is explicitly enabled.</summary>
    public bool Enabled { get; init; }
    public Uri BaseAddress { get; init; } = new("https://jobicy.com/");
    public int PageSize { get; init; } = 10;
    public int MaximumPages { get; init; } = 1;
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    // This is an operator-supplied query mapping, not a declaration of source coverage.
    // The capability registry remains the authority for whether a market is eligible.
    public Dictionary<string, string> MarketGeo { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public void Validate()
    {
        if (!BaseAddress.IsAbsoluteUri || BaseAddress.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Jobicy base address must be an absolute HTTPS URI.", nameof(BaseAddress));
        if (PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(PageSize));
        if (MaximumPages is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(MaximumPages));
        if (RequestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        if (MaximumRetryDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaximumRetryDelay));
        if (MarketGeo is null || MarketGeo.Any(pair => !IsCountryCode(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
            throw new ArgumentException("Market-to-geo mappings must contain country codes and nonempty Jobicy geo values.", nameof(MarketGeo));
    }

    public bool TryGetGeo(CountryCode market, out string geo) =>
        MarketGeo.TryGetValue(market.Value, out geo!);

    private static bool IsCountryCode(string? value) => value is { Length: 2 } && value.All(char.IsAsciiLetter);
}

public interface IJobicyRequestRateLimiter
{
    Task WaitAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Process-local one-request-per-second limiter. Multi-instance deployments must replace this
/// with a shared implementation before enabling the source.
/// </summary>
public sealed class InProcessJobicyRequestRateLimiter(TimeProvider? timeProvider = null) : IJobicyRequestRateLimiter
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private DateTimeOffset _nextRequestAtUtc = DateTimeOffset.MinValue;

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var now = _timeProvider.GetUtcNow();
            var delay = _nextRequestAtUtc - now;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, _timeProvider, cancellationToken);
            _nextRequestAtUtc = _timeProvider.GetUtcNow().AddSeconds(1);
        }
        finally { _mutex.Release(); }
    }
}
