using System.Text.Json;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.MediaTaggingManager.Configuration;

/// <summary>
/// Maintains a server-local recovery copy of administrator settings. Jellyfin's
/// base plugin loader replaces unreadable configuration XML with defaults, so
/// the recovery copy is validated before that fallback can run.
/// </summary>
internal static class ConfigurationRecovery
{
    private const string RecoveryDirectoryName = "media-tagging-manager";
    private const string CurrentSnapshotFileName = "settings-recovery-current.json";
    private const string PreviousSnapshotFileName = "settings-recovery-previous.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Restores a known-good mirror only when Jellyfin's primary XML is missing
    /// or cannot be deserialized. A valid intentionally blank configuration is
    /// never replaced.
    /// </summary>
    public static void RestoreIfNecessary(
        Plugin plugin,
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer)
    {
        if (CanReadPrimaryConfiguration(plugin.ConfigurationFilePath, xmlSerializer))
        {
            return;
        }

        try
        {
            var snapshot = LoadLatestSnapshot(applicationPaths);
            if (snapshot is null)
            {
                return;
            }

            PreserveUnreadableConfiguration(plugin.ConfigurationFilePath, applicationPaths);
            xmlSerializer.SerializeToFile(snapshot, plugin.ConfigurationFilePath);
        }
        catch (IOException)
        {
            // A recovery copy must never prevent Jellyfin from starting. The
            // normal configuration path remains available if disk access fails.
        }
        catch (UnauthorizedAccessException)
        {
            // See the IO exception comment above.
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

    private static bool CanReadPrimaryConfiguration(string configurationPath, IXmlSerializer xmlSerializer)
    {
        if (!File.Exists(configurationPath))
        {
            return false;
        }

        try
        {
            return xmlSerializer.DeserializeFromFile(typeof(PluginConfiguration), configurationPath) is PluginConfiguration;
        }
        catch
        {
            return false;
        }
    }

    private static PluginConfiguration? LoadLatestSnapshot(IApplicationPaths applicationPaths)
    {
        foreach (var fileName in new[] { CurrentSnapshotFileName, PreviousSnapshotFileName })
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
                    return snapshot;
                }
            }
            catch (JsonException)
            {
                // Try the previous atomically saved snapshot below.
            }
        }

        return null;
    }

    private static void PreserveUnreadableConfiguration(string configurationPath, IApplicationPaths applicationPaths)
    {
        if (!File.Exists(configurationPath))
        {
            return;
        }

        var directory = GetRecoveryDirectory(applicationPaths);
        Directory.CreateDirectory(directory);
        var preservedPath = Path.Combine(directory, $"unreadable-settings-{DateTime.UtcNow:yyyyMMddHHmmss}.xml");
        File.Copy(configurationPath, preservedPath, true);
    }

    private static string GetRecoveryDirectory(IApplicationPaths applicationPaths) =>
        Path.Combine(applicationPaths.DataPath, RecoveryDirectoryName);
}
