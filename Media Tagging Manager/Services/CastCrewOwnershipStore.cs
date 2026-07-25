using System.Text.Json;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Stores hidden provenance for only the people assignments and images created by this plugin.</summary>
public sealed class CastCrewOwnershipStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>Records one or more people assignments created on a Jellyfin media item.</summary>
    public async Task RecordAssignmentsAsync(IEnumerable<CastCrewOwnedAssignment> assignments, CancellationToken cancellationToken)
    {
        var values = assignments.ToArray();
        if (values.Length == 0)
        {
            return;
        }

        await UpdateAsync(document =>
        {
            foreach (var value in values)
            {
                if (!document.Assignments.Any(existing => existing.ItemId == value.ItemId && Same(existing, value)))
                {
                    document.Assignments.Add(value);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Records a Jellyfin person primary image saved by this plugin.</summary>
    public async Task RecordImageAsync(CastCrewOwnedImage image, CancellationToken cancellationToken)
    {
        await UpdateAsync(document =>
        {
            document.Images.RemoveAll(existing => string.Equals(existing.PersonName, image.PersonName, StringComparison.OrdinalIgnoreCase));
            document.Images.Add(image);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets a snapshot of all recorded plugin-owned people assignments and images.</summary>
    public async Task<CastCrewOwnershipDocument> GetAsync(CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>Stops tracking assignment records that have been safely removed or deliberately preserved.</summary>
    public Task RemoveAssignmentsAsync(IEnumerable<CastCrewOwnedAssignment> assignments, CancellationToken cancellationToken) =>
        UpdateAsync(document =>
        {
            foreach (var value in assignments)
            {
                document.Assignments.RemoveAll(existing => existing.ItemId == value.ItemId && Same(existing, value));
            }
        }, cancellationToken);

    /// <summary>Stops tracking person image records that have been safely removed or deliberately preserved.</summary>
    public Task RemoveImagesAsync(IEnumerable<CastCrewOwnedImage> images, CancellationToken cancellationToken) =>
        UpdateAsync(document =>
        {
            foreach (var value in images)
            {
                document.Images.RemoveAll(existing => string.Equals(existing.PersonName, value.PersonName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.ImagePath, value.ImagePath, StringComparison.Ordinal));
            }
        }, cancellationToken);

    private async Task UpdateAsync(Action<CastCrewOwnershipDocument> update, CancellationToken cancellationToken)
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

    private static async Task<CastCrewOwnershipDocument> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(OwnershipPath))
        {
            return new CastCrewOwnershipDocument();
        }

        await using var stream = File.OpenRead(OwnershipPath);
        return await JsonSerializer.DeserializeAsync<CastCrewOwnershipDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? new CastCrewOwnershipDocument();
    }

    private static bool Same(CastCrewOwnedAssignment left, CastCrewOwnedAssignment right) =>
        string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
        && left.Type == right.Type
        && string.Equals(left.Role, right.Role, StringComparison.Ordinal);

    private static string OwnershipPath => System.IO.Path.Combine(Plugin.Instance?.DataFolderPath ?? throw new InvalidOperationException("Plugin data folder is unavailable."), "cast-crew-ownership.json");
}

/// <summary>Plugin-owned people additions for exact, conservative future cleanup.</summary>
public sealed class CastCrewOwnedAssignment
{
    /// <summary>Gets or sets the Jellyfin media item that received the person.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the person's displayed name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets Jellyfin's person kind.</summary>
    public Jellyfin.Data.Enums.PersonKind Type { get; set; }

    /// <summary>Gets or sets the cast character or an empty crew role.</summary>
    public string Role { get; set; } = string.Empty;
}

/// <summary>One Jellyfin person primary image whose exact saved path was created by this plugin.</summary>
public sealed class CastCrewOwnedImage
{
    /// <summary>Gets or sets the shared Jellyfin person name.</summary>
    public string PersonName { get; set; } = string.Empty;

    /// <summary>Gets or sets the image path after Jellyfin saved it locally.</summary>
    public string ImagePath { get; set; } = string.Empty;
}

/// <summary>Serialized private provenance document. It is not displayed as media metadata or a Jellyfin tag.</summary>
public sealed class CastCrewOwnershipDocument
{
    /// <summary>Gets or sets assignment records.</summary>
    public List<CastCrewOwnedAssignment> Assignments { get; set; } = [];

    /// <summary>Gets or sets image records.</summary>
    public List<CastCrewOwnedImage> Images { get; set; } = [];
}
