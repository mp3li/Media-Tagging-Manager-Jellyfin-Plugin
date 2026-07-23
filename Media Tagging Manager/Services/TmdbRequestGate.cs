using System.Net;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Globally spaces TMDb calls and temporarily backs off after an HTTP 429 response.</summary>
public sealed class TmdbRequestGate
{
    // TMDb documents an upper anti-bulk limit around 40 requests per second.
    // Leave five requests per second of headroom for server-side variation.
    private static readonly TimeSpan MinimumRequestSpacing = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 35);
    private readonly SemaphoreSlim _scheduleLock = new(1, 1);
    private DateTimeOffset _nextRequestUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _backoffUntilUtc = DateTimeOffset.MinValue;

    /// <summary>Sends a request after the shared rate gate, retrying a temporary 429 response at most twice.</summary>
    public async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await WaitForRequestSlotAsync(cancellationToken).ConfigureAwait(false);
            using var request = requestFactory();
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt == maxAttempts - 1)
            {
                return response;
            }

            var retryDelay = GetRetryDelay(response, attempt);
            response.Dispose();
            await ApplyBackoffAsync(retryDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("TMDb request retry attempts were exhausted.");
    }

    private async Task WaitForRequestSlotAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset scheduledUtc;
        await _scheduleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            scheduledUtc = new[] { now, _nextRequestUtc, _backoffUntilUtc }.Max();
            _nextRequestUtc = scheduledUtc + MinimumRequestSpacing;
        }
        finally
        {
            _scheduleLock.Release();
        }

        var delay = scheduledUtc - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ApplyBackoffAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        var untilUtc = DateTimeOffset.UtcNow + delay;
        await _scheduleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (untilUtc > _backoffUntilUtc)
            {
                _backoffUntilUtc = untilUtc;
            }

            if (_nextRequestUtc < _backoffUntilUtc)
            {
                _nextRequestUtc = _backoffUntilUtc;
            }
        }
        finally
        {
            _scheduleLock.Release();
        }
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var untilDate = date - DateTimeOffset.UtcNow;
            if (untilDate > TimeSpan.Zero)
            {
                return untilDate;
            }
        }

        return TimeSpan.FromSeconds(2 * (attempt + 1));
    }
}
