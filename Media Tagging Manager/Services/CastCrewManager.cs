using Jellyfin.Plugin.MediaTaggingManager.Models;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Fills only missing Jellyfin cast, crew, and person photos from TMDb while preserving existing people data.</summary>
public sealed class CastCrewManager
{
    /// <summary>Displayed crew jobs available for administrator selection.</summary>
    public static readonly string[] AvailableCrewJobs =
    [
        "Director", "Writer", "Screenplay", "Story", "Producer", "Executive Producer",
        "Original Music Composer", "Director of Photography", "Editor", "Casting",
        "Production Design", "Art Direction", "Costume Design", "Makeup Artist", "Sound Designer"
    ];

    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly TmdbAvailabilitySource _tmdb;
    private readonly CastCrewOwnershipStore _ownership;
    private readonly CastCrewStateStore _state;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    /// <summary>Initializes a new instance of the cast-and-crew manager.</summary>
    public CastCrewManager(
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        TmdbAvailabilitySource tmdb,
        CastCrewOwnershipStore ownership,
        CastCrewStateStore state)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _tmdb = tmdb;
        _ownership = ownership;
        _state = state;
    }

    /// <summary>Gets dedicated people-photo scan status for dashboard polling.</summary>
    public CastCrewPhotoProgress GetPhotoProgress() => _state.GetProgress();

    /// <summary>Records that an administrator requested the dedicated photo scan before Jellyfin starts its task worker.</summary>
    public void MarkPhotoScanQueued() => _state.Queue();

    /// <summary>Returns a clear administrator-facing prerequisite error for the explicit photo scan, if any.</summary>
    public string? GetPhotoScanValidationError()
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("The plugin configuration is unavailable.");
        if (!configuration.FillMissingCastPhotos && !configuration.FillMissingCrewPhotos)
        {
            return "Enable Fill Missing Cast Photos or Fill Missing Crew Photos, then save Cast and Crew Settings before scanning for cast and crew photos.";
        }

        if (string.IsNullOrWhiteSpace(configuration.TmdbApiKey))
        {
            return "Save a TMDb API Read Access Token before scanning for cast and crew photos.";
        }

        return (configuration.LibraryIds ?? []).Length == 0
            ? "Select and save one or more libraries before scanning for cast and crew photos."
            : null;
    }

    /// <summary>Fills configured missing cast, crew, and photos as one normal library scan processes an item.</summary>
    public async Task<CastCrewItemResult> ApplyConfiguredAsync(BaseItem item, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("The plugin configuration is unavailable.");
        if (!IsConfigured(configuration))
        {
            return CastCrewItemResult.Empty;
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ApplyCoreAsync(item, configuration, includePeople: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>Runs the explicit missing-person-photo action across all saved selected libraries without adding people.</summary>
    public async Task ScanMissingPhotosAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("The plugin configuration is unavailable.");
        var validationError = GetPhotoScanValidationError();
        if (validationError is not null)
        {
            throw new InvalidOperationException(validationError);
        }

        var libraryIds = configuration.LibraryIds ?? [];
        var items = libraryIds.SelectMany(GetLibraryItems).ToArray();
        _state.Start(items.Length);
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (var index = 0; index < items.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await ApplyCoreAsync(items[index], configuration, includePeople: false, cancellationToken).ConfigureAwait(false);
                _state.RecordItem(result.MissingPhotos, result.TmdbPhotosAvailable, result.PhotosAdded, result.PhotoBytes);
                progress?.Report(items.Length == 0 ? 100 : (index + 1) * 100d / items.Length);
            }

            _state.Complete();
        }
        catch (OperationCanceledException)
        {
            _state.Complete("Cast and crew photo scan was cancelled. Any photos already saved remain available in Jellyfin.");
            throw;
        }
        catch (Exception exception)
        {
            _state.Complete($"Cast and crew photo scan stopped: {exception.Message}");
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>Removes only exact cast/crew assignments recorded as created by this plugin in still-selected libraries.</summary>
    public async Task<CastCrewCleanupResult> RemoveOwnedAssignmentsAsync(CancellationToken cancellationToken)
    {
        var document = await _ownership.GetAsync(cancellationToken).ConfigureAwait(false);
        var selectedLibraries = (Plugin.Instance?.Configuration.LibraryIds ?? []).ToHashSet();
        var removedRecords = new List<CastCrewOwnedAssignment>();
        var removed = 0;
        var itemsChanged = 0;

        foreach (var group in document.Assignments.GroupBy(value => value.ItemId))
        {
            var item = _libraryManager.GetItemById(group.Key);
            if (item is null || !selectedLibraries.Contains(item.GetTopParent().Id))
            {
                continue;
            }

            var people = _libraryManager.GetPeople(item).ToList();
            var before = people.Count;
            foreach (var owned in group)
            {
                var match = people.FirstOrDefault(person => Matches(person, owned));
                if (match is not null)
                {
                    people.Remove(match);
                    removed++;
                }

                removedRecords.Add(owned);
            }

            if (people.Count != before)
            {
                await _libraryManager.UpdatePeopleAsync(item, people, cancellationToken).ConfigureAwait(false);
                itemsChanged++;
            }
        }

        await _ownership.RemoveAssignmentsAsync(removedRecords, cancellationToken).ConfigureAwait(false);
        return new CastCrewCleanupResult(removed, 0, itemsChanged, 0);
    }

    /// <summary>Removes only exact current person primary images whose saved paths were recorded by this plugin.</summary>
    public async Task<CastCrewCleanupResult> RemoveOwnedImagesAsync(CancellationToken cancellationToken)
    {
        var document = await _ownership.GetAsync(cancellationToken).ConfigureAwait(false);
        var selectedPeople = (Plugin.Instance?.Configuration.LibraryIds ?? [])
            .SelectMany(GetLibraryItems)
            .SelectMany(item => _libraryManager.GetPeople(item))
            .Select(person => person.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var handled = new List<CastCrewOwnedImage>();
        var removed = 0;
        var skipped = 0;
        foreach (var owned in document.Images)
        {
            if (!selectedPeople.Contains(owned.PersonName))
            {
                skipped++;
                continue;
            }

            var person = _libraryManager.GetPerson(owned.PersonName);
            if (person is null || !person.HasImage(ImageType.Primary, 0)
                || !string.Equals(person.GetImagePath(ImageType.Primary, 0), owned.ImagePath, StringComparison.Ordinal))
            {
                handled.Add(owned);
                skipped++;
                continue;
            }

            await person.DeleteImageAsync(ImageType.Primary, 0).ConfigureAwait(false);
            handled.Add(owned);
            removed++;
        }

        await _ownership.RemoveImagesAsync(handled, cancellationToken).ConfigureAwait(false);
        return new CastCrewCleanupResult(0, removed, 0, skipped);
    }

    private async Task<CastCrewItemResult> ApplyCoreAsync(BaseItem item, Configuration.PluginConfiguration configuration, bool includePeople, CancellationToken cancellationToken)
    {
        var ids = new ExternalIds(GetProviderId(item, "Tmdb"), GetProviderId(item, "Imdb"), item.GetType().Name);
        if (string.IsNullOrWhiteSpace(ids.Tmdb))
        {
            return CastCrewItemResult.Empty;
        }

        var credits = await _tmdb.GetCreditsAsync(ids, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(credits.Note))
        {
            return CastCrewItemResult.Empty;
        }

        var people = _libraryManager.GetPeople(item).ToList();
        var additions = new List<CastCrewOwnedAssignment>();
        var castAdded = 0;
        var crewAdded = 0;
        if (includePeople && configuration.AddMissingCast)
        {
            var castLimit = Math.Clamp(configuration.MaximumCastMembers, 1, 200);
            var currentCastCount = people.Count(IsCast);
            var nextSort = people.Where(IsCast).Select(person => person.SortOrder ?? 0).DefaultIfEmpty(-1).Max() + 1;
            foreach (var credit in credits.Cast.OrderBy(credit => credit.Order))
            {
                if (currentCastCount >= castLimit)
                {
                    break;
                }

                if (people.Any(person => IsCast(person) && SameName(person.Name, credit.Name)))
                {
                    continue;
                }

                var person = new PersonInfo
                {
                    Name = credit.Name,
                    Role = credit.Character ?? string.Empty,
                    Type = PersonKind.Actor,
                    SortOrder = nextSort++
                };
                people.Add(person);
                additions.Add(new CastCrewOwnedAssignment { ItemId = item.Id, Name = person.Name, Type = person.Type, Role = person.Role });
                currentCastCount++;
                castAdded++;
            }
        }

        if (includePeople && configuration.AddMissingCrew)
        {
            var selectedJobs = (configuration.SelectedCrewJobs ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var credit in credits.Crew.Where(credit => !string.IsNullOrWhiteSpace(credit.Job) && selectedJobs.Contains(credit.Job)))
            {
                var type = CrewType(credit.Job!);
                if (people.Any(person => !IsCast(person) && SameName(person.Name, credit.Name)))
                {
                    continue;
                }

                var person = new PersonInfo { Name = credit.Name, Role = credit.Job!, Type = type, SortOrder = null };
                people.Add(person);
                additions.Add(new CastCrewOwnedAssignment { ItemId = item.Id, Name = person.Name, Type = person.Type, Role = person.Role });
                crewAdded++;
            }
        }

        if (additions.Count > 0)
        {
            await _libraryManager.UpdatePeopleAsync(item, people, cancellationToken).ConfigureAwait(false);
            await _ownership.RecordAssignmentsAsync(additions, cancellationToken).ConfigureAwait(false);
        }

        var photoResult = await FillMissingPhotosAsync(credits, people, configuration, cancellationToken).ConfigureAwait(false);
        return new CastCrewItemResult(castAdded, crewAdded, photoResult.Missing, photoResult.Available, photoResult.Added, photoResult.Bytes);
    }

    private async Task<PhotoResult> FillMissingPhotosAsync(TmdbCreditsResult credits, IReadOnlyCollection<PersonInfo> currentPeople, Configuration.PluginConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!configuration.FillMissingCastPhotos && !configuration.FillMissingCrewPhotos)
        {
            return PhotoResult.Empty;
        }

        var selectedCrewJobs = (configuration.SelectedCrewJobs ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<TmdbPersonCredit>();
        if (configuration.FillMissingCastPhotos)
        {
            candidates.AddRange(credits.Cast);
        }

        if (configuration.FillMissingCrewPhotos)
        {
            candidates.AddRange(credits.Crew.Where(credit => !string.IsNullOrWhiteSpace(credit.Job) && selectedCrewJobs.Contains(credit.Job)));
        }

        var missing = 0;
        var available = 0;
        var added = 0;
        long bytes = 0;
        foreach (var credit in candidates.GroupBy(credit => credit.Name, StringComparer.OrdinalIgnoreCase).Select(group => group.First()))
        {
            if (!currentPeople.Any(person => SameName(person.Name, credit.Name)))
            {
                continue;
            }

            var person = _libraryManager.GetPerson(credit.Name);
            if (person is null || person.HasImage(ImageType.Primary, 0))
            {
                continue;
            }

            missing++;
            if (string.IsNullOrWhiteSpace(credit.ProfilePath))
            {
                continue;
            }

            available++;
            TmdbPersonImage? image;
            try
            {
                image = await _tmdb.DownloadPersonImageAsync(credit.ProfilePath, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                continue;
            }
            if (image is null)
            {
                continue;
            }

            try
            {
                await using var stream = new MemoryStream(image.Content, writable: false);
                await _providerManager.SaveImage(person, stream, image.ContentType, ImageType.Primary, null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or ArgumentException)
            {
                continue;
            }
            var savedPath = person.GetImagePath(ImageType.Primary, 0);
            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                await _ownership.RecordImageAsync(new CastCrewOwnedImage { PersonName = person.Name, ImagePath = savedPath }, cancellationToken).ConfigureAwait(false);
            }

            added++;
            bytes += image.SourceBytes;
        }

        return new PhotoResult(missing, available, added, bytes);
    }

    private IEnumerable<BaseItem> GetLibraryItems(Guid libraryId) => _libraryManager.GetItemList(new InternalItemsQuery
    {
        ParentId = libraryId,
        Recursive = true,
        IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series]
    });

    private static bool IsConfigured(Configuration.PluginConfiguration configuration) => configuration.AddMissingCast
        || configuration.AddMissingCrew
        || configuration.FillMissingCastPhotos
        || configuration.FillMissingCrewPhotos;

    private static bool IsCast(PersonInfo person) => person.Type is PersonKind.Actor or PersonKind.GuestStar;

    private static bool Matches(PersonInfo person, CastCrewOwnedAssignment owned) => SameName(person.Name, owned.Name)
        && person.Type == owned.Type
        && string.Equals(person.Role ?? string.Empty, owned.Role, StringComparison.Ordinal);

    private static bool SameName(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static PersonKind CrewType(string job) => job switch
    {
        "Director" => PersonKind.Director,
        "Writer" or "Screenplay" or "Story" => PersonKind.Writer,
        "Producer" or "Executive Producer" => PersonKind.Producer,
        "Original Music Composer" => PersonKind.Composer,
        "Editor" => PersonKind.Editor,
        _ => PersonKind.Unknown
    };

    private static string? GetProviderId(BaseItem item, string name) => item.ProviderIds.TryGetValue(name, out var value) ? value : null;

    /// <summary>Counts work done for one item without exposing private source/person identifiers to the dashboard.</summary>
    public sealed record CastCrewItemResult(int CastAdded, int CrewAdded, int MissingPhotos, int TmdbPhotosAvailable, int PhotosAdded, long PhotoBytes)
    {
        /// <summary>Gets an empty result.</summary>
        public static CastCrewItemResult Empty { get; } = new(0, 0, 0, 0, 0, 0);
    }

    private sealed record PhotoResult(int Missing, int Available, int Added, long Bytes)
    {
        public static PhotoResult Empty { get; } = new(0, 0, 0, 0);
    }
}
