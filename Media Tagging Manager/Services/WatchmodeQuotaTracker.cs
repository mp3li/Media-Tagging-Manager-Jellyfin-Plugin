using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Serializes and persists the administrator-selected Watchmode 30-day quota allowance.</summary>
public sealed class WatchmodeQuotaTracker
{
    private readonly object _lock = new();

    /// <summary>Reserves request credits before a Watchmode title lookup starts.</summary>
    public bool TryReserve(int credits, out string? reason)
    {
        lock (_lock)
        {
            var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
            if (!TryGetCurrentCycle(configuration, out var cycleStart, out _))
            {
                reason = "Set Watchmode's Quota Resets On date in API Settings before using Watchmode.";
                return false;
            }

            ResetCycleIfNeeded(configuration, cycleStart);
            var limit = Math.Max(0, configuration.WatchmodeMonthlyLimit);
            if (configuration.WatchmodeRequestsUsed + credits > limit)
            {
                reason = "The configured Watchmode request limit has been reached for the current 30-day cycle.";
                return false;
            }

            configuration.WatchmodeRequestsUsed += credits;
            Plugin.Instance?.SaveConfiguration(configuration);
            reason = null;
            return true;
        }
    }

    /// <summary>Records the authoritative quota headers returned by Watchmode when available.</summary>
    public void RecordServerUsage(int? used)
    {
        lock (_lock)
        {
            var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
            if (!TryGetCurrentCycle(configuration, out var cycleStart, out _))
            {
                return;
            }

            ResetCycleIfNeeded(configuration, cycleStart);
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
            if (!TryGetCurrentCycle(configuration, out var cycleStart, out var resetsOn))
            {
                return new WatchmodeUsageDto(0, Math.Max(0, configuration.WatchmodeMonthlyLimit), string.Empty, string.Empty, false, false);
            }

            if (ResetCycleIfNeeded(configuration, cycleStart))
            {
                Plugin.Instance?.SaveConfiguration(configuration);
            }

            var limit = Math.Max(0, configuration.WatchmodeMonthlyLimit);
            return new WatchmodeUsageDto(configuration.WatchmodeRequestsUsed, limit, cycleStart, resetsOn, true, configuration.WatchmodeRequestsUsed >= limit);
        }
    }

    private static bool ResetCycleIfNeeded(Configuration.PluginConfiguration configuration, string cycleStart)
    {
        if (!string.Equals(configuration.WatchmodeUsageCycleStart, cycleStart, StringComparison.Ordinal))
        {
            configuration.WatchmodeUsageCycleStart = cycleStart;
            configuration.WatchmodeRequestsUsed = 0;
            return true;
        }

        return false;
    }

    private static bool TryGetCurrentCycle(Configuration.PluginConfiguration configuration, out string cycleStart, out string resetsOn)
    {
        cycleStart = string.Empty;
        resetsOn = string.Empty;
        if (!DateOnly.TryParseExact(
                configuration.WatchmodeQuotaResetsOn,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var resetDate))
        {
            return false;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        while (today >= resetDate)
        {
            resetDate = resetDate.AddDays(30);
        }

        while (today < resetDate.AddDays(-30))
        {
            resetDate = resetDate.AddDays(-30);
        }

        cycleStart = resetDate.AddDays(-30).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        resetsOn = resetDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }
}
