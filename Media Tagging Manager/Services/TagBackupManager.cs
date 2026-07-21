using System.Text.Json;
using Jellyfin.Plugin.MediaTaggingManager.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Creates and restores complete tag snapshots in Jellyfin's plugin data directory.</summary>
public sealed class TagBackupManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ILibraryManager _libraryManager;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="TagBackupManager"/> class.</summary>
    public TagBackupManager(ILibraryManager libraryManager) => _libraryManager = libraryManager;

    /// <summary>Creates a complete tag snapshot for every item in the supplied libraries.</summary>
    public async Task<TagBackupSummary> CreateAsync(string label, IEnumerable<Guid> libraryIds, CancellationToken cancellationToken)
    {
        var document = new TagBackupDocument
        {
            Id = Guid.NewGuid(),
            Label = label.Trim(),
            CreatedUtc = DateTimeOffset.UtcNow
        };

        var seen = new HashSet<Guid>();
        foreach (var libraryId in libraryIds.Distinct())
        {
            var items = _libraryManager.GetItemList(new InternalItemsQuery { ParentId = libraryId, Recursive = true });
            foreach (var item in items.Where(item => seen.Add(item.Id)))
            {
                document.Items.Add(ToBackupItem(item));
            }
        }

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(BackupDirectory);
            await File.WriteAllTextAsync(BackupPath(document.Id), JsonSerializer.Serialize(document, JsonOptions), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }

        return ToSummary(document);
    }

    /// <summary>Gets available backups, newest first.</summary>
    public async Task<IReadOnlyCollection<TagBackupSummary>> GetAllAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(BackupDirectory))
        {
            return [];
        }

        var results = new List<TagBackupSummary>();
        foreach (var path in Directory.EnumerateFiles(BackupDirectory, "tag-backup-*.json", SearchOption.TopDirectoryOnly))
        {
            var document = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
            results.Add(ToSummary(document));
        }

        return results.OrderByDescending(backup => backup.CreatedUtc).ToArray();
    }

    /// <summary>Restores every captured tag list from a specific backup. Items removed from Jellyfin are skipped.</summary>
    public async Task<TagBackupSummary> RestoreAsync(Guid backupId, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var document = await ReadAsync(BackupPath(backupId), cancellationToken).ConfigureAwait(false);
        var completed = 0;
        foreach (var savedItem in document.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = _libraryManager.GetItemById(savedItem.ItemId);
            if (item is not null)
            {
                item.Tags = savedItem.Tags;
                await _libraryManager.UpdateItemAsync(item, item, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
            }

            completed++;
            progress?.Report(document.Items.Count == 0 ? 100 : completed * 100d / document.Items.Count);
        }

        return ToSummary(document);
    }

    /// <summary>Restores the newest available backup and returns its summary.</summary>
    public async Task<TagBackupSummary> UndoLatestAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var latest = (await GetAllAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
            ?? throw new InvalidOperationException("No tag backups are available to restore.");
        return await RestoreAsync(latest.Id, progress, cancellationToken).ConfigureAwait(false);
    }

    private static TagBackupItem ToBackupItem(BaseItem item) => new()
    {
        ItemId = item.Id,
        Name = item.Name,
        ItemType = item.GetType().Name,
        Tags = item.Tags ?? []
    };

    private static TagBackupSummary ToSummary(TagBackupDocument document) =>
        new(document.Id, document.Label, document.CreatedUtc, document.Items.Count);

    private static async Task<TagBackupDocument> ReadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new KeyNotFoundException("The requested tag backup does not exist.");
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<TagBackupDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The tag backup is empty or invalid.");
    }

    private static string BackupDirectory => Path.Combine(Plugin.Instance?.DataFolderPath ?? throw new InvalidOperationException("Plugin data folder is unavailable."), "tag-backups");

    private static string BackupPath(Guid id) => Path.Combine(BackupDirectory, $"tag-backup-{id:N}.json");
}
