using System.Text.Json;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Persists only native Studio and production-country values this plugin added, so cleanup never starts from all Jellyfin metadata.</summary>
public sealed class ProductionOwnershipStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Gets the current ownership document.</summary>
    public async Task<ProductionOwnershipDocument> GetAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(Path))
            {
                return new ProductionOwnershipDocument();
            }

            await using var stream = File.OpenRead(Path);
            return await JsonSerializer.DeserializeAsync<ProductionOwnershipDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? new ProductionOwnershipDocument();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Records values that were successfully added to native Jellyfin metadata.</summary>
    public async Task RecordAsync(IEnumerable<ProductionOwnedValue> values, CancellationToken cancellationToken)
    {
        var incoming = values.Where(value => !string.IsNullOrWhiteSpace(value.Name)).ToArray();
        if (incoming.Length == 0)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadLockedAsync(cancellationToken).ConfigureAwait(false);
            foreach (var value in incoming)
            {
                if (!document.Values.Any(existing => existing.ItemId == value.ItemId
                    && existing.Kind == value.Kind
                    && string.Equals(existing.Name, value.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    document.Values.Add(value);
                }
            }

            await WriteLockedAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Removes ownership records after their corresponding native values have been removed.</summary>
    public async Task RemoveAsync(IEnumerable<ProductionOwnedValue> values, CancellationToken cancellationToken)
    {
        var lookup = values.Select(value => (value.ItemId, value.Kind, Name: value.Name.Trim())).ToHashSet();
        if (lookup.Count == 0)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadLockedAsync(cancellationToken).ConfigureAwait(false);
            document.Values.RemoveAll(value => lookup.Contains((value.ItemId, value.Kind, value.Name.Trim())));
            await WriteLockedAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string Path => System.IO.Path.Combine(Plugin.Instance?.DataFolderPath ?? throw new InvalidOperationException("Plugin data folder is unavailable."), "production-ownership.json");

    private static async Task<ProductionOwnershipDocument> ReadLockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(Path))
        {
            return new ProductionOwnershipDocument();
        }

        await using var stream = File.OpenRead(Path);
        return await JsonSerializer.DeserializeAsync<ProductionOwnershipDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? new ProductionOwnershipDocument();
    }

    private static async Task WriteLockedAsync(ProductionOwnershipDocument document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        await File.WriteAllTextAsync(Path, JsonSerializer.Serialize(document, JsonOptions), cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Persisted production-metadata provenance.</summary>
public sealed class ProductionOwnershipDocument
{
    /// <summary>Gets or sets plugin-owned native metadata values.</summary>
    public List<ProductionOwnedValue> Values { get; set; } = [];
}

/// <summary>One native production metadata value added by this plugin.</summary>
public sealed class ProductionOwnedValue
{
    /// <summary>Gets or sets the Jellyfin item identifier.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the selected library identifier.</summary>
    public Guid LibraryId { get; set; }

    /// <summary>Gets or sets either Company or Country.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable native metadata value.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the ISO code for an owned production country.</summary>
    public string? CountryCode { get; set; }

    /// <summary>Gets or sets the source-supplied logo URL for an owned production company.</summary>
    public string? LogoUrl { get; set; }
}
