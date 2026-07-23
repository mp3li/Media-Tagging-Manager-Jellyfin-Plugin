using System.Text.Json;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.MediaTaggingManager.Configuration;

/// <summary>
/// Maintains a server-local recovery copy of administrator settings. Jellyfin's
/// base plugin loader replaces unreadable configuration XML with defaults. Some
/// installation paths can also leave a valid but empty default XML, so both
/// cases are validated before that fallback can discard saved settings.
/// </summary>
internal static class ConfigurationRecovery
{
    private const string RecoveryDirectoryName = "media-tagging-manager";
    private const string CurrentSnapshotFileName = "settings-recovery-current.json";
    private const string PreviousSnapshotFileName = "settings-recovery-previous.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string _status = "Settings recovery has not run yet.";

    /// <summary>Gets a non-sensitive summary of the configuration recovery decision made at startup.</summary>
    public static string Status => _status;

    /// <summary>
    /// Restores the best known-good mirror when the primary XML is missing,
    /// unreadable, or a new blank default that conflicts with saved settings.
    /// </summary>
    public static void RestoreIfNecessary(
        Plugin plugin,
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer)
    {
        try
        {
            var primary = TryLoadPrimaryConfiguration(plugin.ConfigurationFilePath, xmlSerializer);
            var snapshots = LoadSnapshots(applicationPaths);
            var current = snapshots.FirstOrDefault(snapshot => snapshot.IsCurrent);
            var snapshot = SelectRecoverySnapshot(snapshots);
            if (snapshot is null)
            {
                _status = primary is null
                    ? "No prior settings recovery copy was available."
                    : "Current settings loaded normally; no recovery copy was needed.";
                return;
            }

            if (primary is not null && !ShouldRestoreBlankPrimary(primary, current, snapshot.Configuration))
            {
                _status = "Current settings loaded normally; no recovery copy was needed.";
                return;
            }

            PreserveReplacedConfiguration(plugin.ConfigurationFilePath, applicationPaths);
            xmlSerializer.SerializeToFile(snapshot.Configuration, plugin.ConfigurationFilePath);
            _status = $"Restored saved settings from the {(snapshot.IsCurrent ? "current" : "previous")} server-local recovery copy.";
        }
        catch (IOException)
        {
            // A recovery copy must never prevent Jellyfin from starting. The
            // normal configuration path remains available if disk access fails.
            _status = "Settings recovery could not access the server-local recovery copy.";
        }
        catch (UnauthorizedAccessException)
        {
            // See the IO exception comment above.
            _status = "Settings recovery could not access the server-local recovery copy.";
        }
    }

    /// <summary>Saves an atomic recovery copy after Jellyfin has persisted settings.</summary>
    public static void Save(IApplicationPaths applicationPaths, PluginConfiguration configuration)
    {
        try
        {
            var directory = GetRecoveryDirectory(applicationPaths);
            Directory.CreateDirectory(directory);

            var currentPath = Path.Combine(directory, CurrentSnapshotFileName);
            var previousPath = Path.Combine(directory, PreviousSnapshotFileName);
            var temporaryPath = Path.Combine(directory, $"{CurrentSnapshotFileName}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(configuration, JsonOptions));
            if (File.Exists(currentPath))
            {
                File.Copy(currentPath, previousPath, true);
            }

            File.Move(temporaryPath, currentPath, true);
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (IOException)
        {
            // The primary Jellyfin configuration has already been saved. Do
            // not report that save as failed solely because a recovery mirror
            // could not be refreshed.
        }
        catch (UnauthorizedAccessException)
        {
            // See the IO exception comment above.
        }
    }

    private static PluginConfiguration? TryLoadPrimaryConfiguration(string configurationPath, IXmlSerializer xmlSerializer)
    {
        if (!File.Exists(configurationPath))
        {
            return null;
        }

        try
        {
            return xmlSerializer.DeserializeFromFile(typeof(PluginConfiguration), configurationPath) as PluginConfiguration;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyCollection<RecoverySnapshot> LoadSnapshots(IApplicationPaths applicationPaths)
    {
        var snapshots = new List<RecoverySnapshot>();
        foreach (var (fileName, isCurrent) in new[] { (CurrentSnapshotFileName, true), (PreviousSnapshotFileName, false) })
        {
            var path = Path.Combine(GetRecoveryDirectory(applicationPaths), fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var snapshot = JsonSerializer.Deserialize<PluginConfiguration>(File.ReadAllText(path), JsonOptions);
                if (snapshot is not null)
                {
                    snapshots.Add(new RecoverySnapshot(snapshot, isCurrent, File.GetLastWriteTimeUtc(path)));
                }
            }
            catch (JsonException)
            {
                // Try the previous atomically saved snapshot below.
            }
        }

        return snapshots;
    }

    private static RecoverySnapshot? SelectRecoverySnapshot(IEnumerable<RecoverySnapshot> snapshots) =>
        snapshots.OrderByDescending(snapshot => SettingsScore(snapshot.Configuration))
            .ThenByDescending(snapshot => snapshot.SavedUtc)
            .FirstOrDefault();

    private static bool ShouldRestoreBlankPrimary(
        PluginConfiguration primary,
        RecoverySnapshot? current,
        PluginConfiguration recovery)
    {
        if (!IsEffectivelyBlank(primary))
        {
            return false;
        }

        // An administrator can intentionally clear every setting. A dashboard
        // save marks the primary XML and current recovery copy with the same
        // explicit save time, so preserve that deliberate blank state.
        if (primary.LastSettingsSavedUtc != default
            && current is not null
            && current.Configuration.LastSettingsSavedUtc == primary.LastSettingsSavedUtc)
        {
            return false;
        }

        return SettingsScore(recovery) > SettingsScore(primary);
    }

    private static bool IsEffectivelyBlank(PluginConfiguration configuration) => SettingsScore(configuration) == 0;

    private static int SettingsScore(PluginConfiguration configuration)
    {
        var score = 0;
        score += configuration.LibraryIds.Length * 8;
        score += string.IsNullOrWhiteSpace(configuration.TmdbApiKey) ? 0 : 32;
        score += string.IsNullOrWhiteSpace(configuration.WatchmodeApiKey) ? 0 : 32;
        score += configuration.Regions.Length * 4;
        score += string.Equals(configuration.Region, "US", StringComparison.OrdinalIgnoreCase) ? 0 : 2;
        score += configuration.SelectedProviderNames.Length * 2;
        score += configuration.SelectedNetworkNames.Length * 2;
        score += configuration.SelectedGenreNames.Length * 2;
        score += configuration.UnknownTagMappings.Count * 2;
        score += configuration.KnownProviderNames.Length;
        score += configuration.KnownNetworkNames.Length;
        score += configuration.SaveTagsToJellyfin ? 0 : 2;
        score += configuration.SaveTagsToNfoFiles ? 2 : 0;
        score += configuration.TagProviders ? 0 : 2;
        score += configuration.TagNetworks ? 0 : 2;
        score += configuration.TagGenres ? 2 : 0;
        score += configuration.TagKeywords ? 2 : 0;
        score += string.Equals(configuration.TvNetworkAppTaggingMode, "NetworkOnly", StringComparison.Ordinal) ? 0 : 2;
        score += configuration.EnableAutomaticRefresh ? 2 : 0;
        score += configuration.EnableNewMediaChecks ? 2 : 0;
        score += configuration.RestrictProvidersToSelected ? 2 : 0;
        score += configuration.RestrictNetworksToSelected ? 2 : 0;
        score += configuration.ReplaceManagedTags ? 0 : 2;
        score += configuration.EnableLogoCaching ? 0 : 2;
        score += configuration.RefreshIntervalHours == 168 ? 0 : 1;
        score += configuration.LogoCacheLimitMegabytes == 100 ? 0 : 1;
        score += configuration.WatchmodeMonthlyLimit == 2500 ? 0 : 1;
        score += string.IsNullOrWhiteSpace(configuration.WatchmodeQuotaResetsOn) ? 0 : 2;
        score += configuration.WatchmodeRequestsUsed > 0 ? 2 : 0;
        return score;
    }

    private static void PreserveReplacedConfiguration(string configurationPath, IApplicationPaths applicationPaths)
    {
        if (!File.Exists(configurationPath))
        {
            return;
        }

        var directory = GetRecoveryDirectory(applicationPaths);
        Directory.CreateDirectory(directory);
        var preservedPath = Path.Combine(directory, $"replaced-settings-{DateTime.UtcNow:yyyyMMddHHmmss}.xml");
        File.Copy(configurationPath, preservedPath, true);
    }

    private static string GetRecoveryDirectory(IApplicationPaths applicationPaths) =>
        Path.Combine(applicationPaths.DataPath, RecoveryDirectoryName);

    private sealed record RecoverySnapshot(PluginConfiguration Configuration, bool IsCurrent, DateTime SavedUtc);
}
