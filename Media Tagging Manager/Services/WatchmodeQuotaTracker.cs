using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Serializes and persists the administrator-selected monthly Watchmode allowance.</summary>
public sealed class WatchmodeQuotaTracker
{
    private readonly object _lock = new();

    /// <summary>Reserves request credits before a Watchmode title lookup starts.</summary>
    public bool TryReserve(int credits)
    {
        lock (_lock)
        {
            var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
            ResetMonthIfNeeded(configuration);
            var limit = Math.Max(0, configuration.WatchmodeMonthlyLimit);
            if (configuration.WatchmodeRequestsUsed + credits > limit)
            {
                return false;
            }

            configuration.WatchmodeRequestsUsed += credits;
            Plugin.Instance?.SaveConfiguration(configuration);
            return true;
        }
    }

    /// <summary>Records the authoritative quota headers returned by Watchmode when available.</summary>
    public void RecordServerUsage(int? used)
    {
        lock (_lock)
        {
            var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
            ResetMonthIfNeeded(configuration);
            if (used is { } serverUsed)
            {
                configuration.WatchmodeRequestsUsed = Math.Max(0, serverUsed);
            }

            Plugin.Instance?.SaveConfiguration(configuration);
        }
    }

    /// <summary>Gets current tracked Watchmode usage for the dashboard.</summary>
    public WatchmodeUsageDto GetUsage()
    {
        lock (_lock)
        {
            var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
            ResetMonthIfNeeded(configuration);
            var limit = Math.Max(0, configuration.WatchmodeMonthlyLimit);
            return new WatchmodeUsageDto(configuration.WatchmodeRequestsUsed, limit, configuration.WatchmodeUsageMonth, configuration.WatchmodeRequestsUsed >= limit);
        }
    }

    private static void ResetMonthIfNeeded(Configuration.PluginConfiguration configuration)
    {
        var month = DateTime.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        if (!string.Equals(configuration.WatchmodeUsageMonth, month, StringComparison.Ordinal))
        {
            configuration.WatchmodeUsageMonth = month;
            configuration.WatchmodeRequestsUsed = 0;
        }
    }
}
