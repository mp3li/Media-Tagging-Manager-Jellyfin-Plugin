using System.Text.Json;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Stores exact tag values added by this plugin so source and NFO tags are never inferred to be plugin-owned.</summary>
public sealed class TagOwnershipStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>Gets exact tag values recorded as added by this plugin for one Jellyfin item.</summary>
    public async Task<IReadOnlyCollection<TagOwnedValue>> GetForItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return document.Values.Where(value => value.ItemId == itemId).ToArray();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>Records only tag values that were absent before this plugin successfully added them.</summary>
    public Task RecordAsync(IEnumerable<TagOwnedValue> values, CancellationToken cancellationToken)
    {
        var incoming = values.Where(value => !string.IsNullOrWhiteSpace(value.Tag)).ToArray();
        if (incoming.Length == 0)
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(document =>
        {
            foreach (var value in incoming)
            {
                if (!document.Values.Any(existing => existing.ItemId == value.ItemId
                    && string.Equals(existing.Tag, value.Tag, StringComparison.OrdinalIgnoreCase)))
                {
                    document.Values.Add(value);
                }
            }
        }, cancellationToken);
    }

    /// <summary>Stops tracking tag values that were removed or are no longer present.</summary>
    public Task RemoveAsync(Guid itemId, IEnumerable<string> tags, CancellationToken cancellationToken)
    {
        var removed = tags.Where(static tag => !string.IsNullOrWhiteSpace(tag)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (removed.Count == 0)
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(document => document.Values.RemoveAll(value => value.ItemId == itemId && removed.Contains(value.Tag)), cancellationToken);
    }

    /// <summary>Clears ownership claims before complete tag lists are restored from a backup.</summary>
    public Task ClearForItemsAsync(IEnumerable<Guid> itemIds, CancellationToken cancellationToken)
    {
        var restoredItems = itemIds.ToHashSet();
        if (restoredItems.Count == 0)
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(document => document.Values.RemoveAll(value => restoredItems.Contains(value.ItemId)), cancellationToken);
    }

    private async Task UpdateAsync(Action<TagOwnershipDocument> update, CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadAsync(cancellationToken).ConfigureAwait(false);
            update(document);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(OwnershipPath)!);
            await using var stream = File.Create(OwnershipPath);
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static async Task<TagOwnershipDocument> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(OwnershipPath))
        {
            return new TagOwnershipDocument();
        }

        await using var stream = File.OpenRead(OwnershipPath);
        return await JsonSerializer.DeserializeAsync<TagOwnershipDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? new TagOwnershipDocument();
    }

    private static string OwnershipPath => System.IO.Path.Combine(Plugin.Instance?.DataFolderPath ?? throw new InvalidOperationException("Plugin data folder is unavailable."), "tag-ownership.json");
}

/// <summary>Persisted provenance for tags actually added by this plugin.</summary>
public sealed class TagOwnershipDocument
{
    /// <summary>Gets or sets exact plugin-owned tag values.</summary>
    public List<TagOwnedValue> Values { get; set; } = [];
}

/// <summary>One exact Jellyfin tag value added by this plugin.</summary>
public sealed class TagOwnedValue
{
    /// <summary>Gets or sets the Jellyfin media item identifier.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the selected library identifier.</summary>
    public Guid LibraryId { get; set; }

    /// <summary>Gets or sets the complete tag string, including its managed prefix.</summary>
    public string Tag { get; set; } = string.Empty;
}
