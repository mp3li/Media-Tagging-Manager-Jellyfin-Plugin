#pragma warning disable CS1591
using System.Text.Json;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MediaTaggingManager.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Stores TMDb rating/classification and language/translation data while using native Jellyfin fields where they exist.</summary>
public sealed class SupplementalMetadataManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ILibraryManager _libraryManager;
    private readonly TmdbAvailabilitySource _tmdb;
    private readonly TagDestinationWriter _writer;
    private readonly SupplementalMetadataStateStore _state;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HashSet<Guid> _ratingsLatestChanges = [];
    private readonly HashSet<Guid> _languagesLatestChanges = [];

    public SupplementalMetadataManager(ILibraryManager libraryManager, TmdbAvailabilitySource tmdb, TagDestinationWriter writer, SupplementalMetadataStateStore state)
    {
        _libraryManager = libraryManager;
        _tmdb = tmdb;
        _writer = writer;
        _state = state;
    }

    /// <summary>Gets whether a full scan has enabled rating/classification work.</summary>
    public static bool IsRatingsConfigured(Configuration.PluginConfiguration configuration) => configuration.AddCommunityRatings || configuration.SaveVoteCounts || configuration.AddAgeRatings || configuration.SaveAdultFlags;

    /// <summary>Gets whether a full scan has enabled language/translation work.</summary>
    public static bool IsLanguagesConfigured(Configuration.PluginConfiguration configuration) => configuration.SaveOriginalLanguages || configuration.SaveSpokenLanguages || configuration.SaveAvailableTranslations;

    /// <summary>Validates a dedicated selected-library action.</summary>
    public string? GetValidationError(bool ratings)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("The plugin configuration is unavailable.");
        if (!(ratings ? IsRatingsConfigured(configuration) : IsLanguagesConfigured(configuration)))
        {
            return ratings ? "Enable at least one Ratings Settings option and save before loading ratings and classifications." : "Enable at least one Spoken Languages and Translations option and save before loading languages and translations.";
        }
        if (string.IsNullOrWhiteSpace(configuration.TmdbApiKey)) return "Save a TMDb API Read Access Token in Main Settings before loading this metadata.";
        return configuration.LibraryIds.Length == 0 ? "Select and save one or more libraries in Main Settings before loading this metadata." : null;
    }

    /// <summary>Runs only one selected supplementary-metadata category for all selected libraries.</summary>
    public async Task ScanConfiguredLibrariesAsync(bool ratings, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var error = GetValidationError(ratings);
        if (error is not null) throw new InvalidOperationException(error);
        var configuration = Plugin.Instance!.Configuration;
        var candidates = configuration.LibraryIds.SelectMany(libraryId => GetLibraryItems(libraryId).Select(item => (Item: item, LibraryId: libraryId))).ToArray();
        if (ratings) _ratingsLatestChanges.Clear(); else _languagesLatestChanges.Clear();
        _state.Start(ratings, candidates.Length);
        try
        {
            for (var index = 0; index < candidates.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outcome = await ApplyAsync(candidates[index].Item, candidates[index].LibraryId, ratings, cancellationToken).ConfigureAwait(false);
                _state.Record(ratings, outcome);
                progress?.Report(candidates.Length == 0 ? 100 : (index + 1) * 100d / candidates.Length);
            }
            _state.Complete(ratings);
        }
        catch (OperationCanceledException)
        {
            _state.Complete(ratings, "The action was cancelled. Metadata already saved remains available.");
            throw;
        }
        catch (Exception exception)
        {
            _state.Complete(ratings, $"The action stopped: {exception.Message}");
            throw;
        }
    }

    /// <summary>Applies configured Ratings and Languages features during the normal full scan.</summary>
    internal async Task ApplyConfiguredAsync(BaseItem item, Guid libraryId, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null || item is not Movie && item is not Series || (!IsRatingsConfigured(configuration) && !IsLanguagesConfigured(configuration))) return;
        await ApplyAsync(item, libraryId, ratings: true, cancellationToken).ConfigureAwait(false);
        await ApplyAsync(item, libraryId, ratings: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns current selected-library rating/classification records.</summary>
    public async Task<IReadOnlyCollection<RatingsOverviewItemDto>> GetRatingsOverviewAsync(CancellationToken cancellationToken)
    {
        var document = await ReadAsync(cancellationToken).ConfigureAwait(false);
        var selected = (Plugin.Instance?.Configuration.LibraryIds ?? []).ToHashSet();
        return document.Items.Where(item => selected.Contains(item.LibraryId)).Select(item => new RatingsOverviewItemDto(item.ItemId, item.Name, item.ItemType, item.LibraryId, item.CommunityRating, item.VoteCount, item.OfficialRating, item.Adult, item.Classifications, _ratingsLatestChanges.Contains(item.ItemId))).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Returns current selected-library language and translation records.</summary>
    public async Task<IReadOnlyCollection<LanguagesOverviewItemDto>> GetLanguagesOverviewAsync(CancellationToken cancellationToken)
    {
        var document = await ReadAsync(cancellationToken).ConfigureAwait(false);
        var selected = (Plugin.Instance?.Configuration.LibraryIds ?? []).ToHashSet();
        return document.Items.Where(item => selected.Contains(item.LibraryId)).Select(item => new LanguagesOverviewItemDto(item.ItemId, item.Name, item.ItemType, item.LibraryId, item.OriginalLanguage, item.SpokenLanguages, item.Translations, _languagesLatestChanges.Contains(item.ItemId))).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Removes plugin-retained supplemental records for selected libraries; native rating fields are deliberately preserved.</summary>
    public async Task<int> RemoveRecordsAsync(bool ratings, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var selected = (Plugin.Instance?.Configuration.LibraryIds ?? []).ToHashSet();
            var count = 0;
            foreach (var item in document.Items.Where(item => selected.Contains(item.LibraryId)))
            {
                if (ratings)
                {
                    if (item.CommunityRating is not null || item.VoteCount is not null || item.Adult is not null || item.Classifications.Count > 0) { item.CommunityRating = null; item.VoteCount = null; item.Adult = null; item.OfficialRating = null; item.Classifications = []; _ratingsLatestChanges.Add(item.ItemId); count++; }
                }
                else if (item.OriginalLanguage is not null || item.SpokenLanguages.Count > 0 || item.Translations.Count > 0)
                {
                    item.OriginalLanguage = null; item.SpokenLanguages = []; item.Translations = []; _languagesLatestChanges.Add(item.ItemId); count++;
                }
            }
            await WriteUnsafeAsync(document, cancellationToken).ConfigureAwait(false);
            return count;
        }
        finally { _lock.Release(); }
    }

    /// <summary>Removes only the requested plugin-retained spoken-language or translation values for selected libraries.</summary>
    public async Task<int> RemoveLanguageRecordsAsync(bool spokenLanguages, bool translations, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var selected = (Plugin.Instance?.Configuration.LibraryIds ?? []).ToHashSet();
            var count = 0;
            foreach (var item in document.Items.Where(item => selected.Contains(item.LibraryId)))
            {
                var changed = false;
                if (spokenLanguages && item.SpokenLanguages.Count > 0) { item.SpokenLanguages = []; changed = true; }
                if (translations && item.Translations.Count > 0) { item.Translations = []; changed = true; }
                if (changed) { _languagesLatestChanges.Add(item.ItemId); count++; }
            }
            await WriteUnsafeAsync(document, cancellationToken).ConfigureAwait(false);
            return count;
        }
        finally { _lock.Release(); }
    }

    /// <summary>Removes only saved country classifications not present in the current saved selection.</summary>
    public async Task<int> SyncClassificationsToSelectionAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
            var selectedLibraries = (configuration.LibraryIds ?? []).ToHashSet();
            var allowed = (configuration.SelectedClassificationCountryCodes ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var count = 0;
            foreach (var item in document.Items.Where(item => selectedLibraries.Contains(item.LibraryId)))
            {
                var retained = item.Classifications.Where(value => allowed.Contains(value.CountryCode)).ToList();
                if (!SameClassifications(item.Classifications, retained)) { item.Classifications = retained; _ratingsLatestChanges.Add(item.ItemId); count++; }
            }
            await WriteUnsafeAsync(document, cancellationToken).ConfigureAwait(false);
            return count;
        }
        finally { _lock.Release(); }
    }

    private async Task<SupplementalOutcome> ApplyAsync(BaseItem item, Guid libraryId, bool ratings, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance!.Configuration;
        if (!(ratings ? IsRatingsConfigured(configuration) : IsLanguagesConfigured(configuration))) return SupplementalOutcome.NotConfigured;
        var tmdbId = item.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;
        if (string.IsNullOrWhiteSpace(tmdbId)) return SupplementalOutcome.MissingId;
        TmdbSupplementalMetadataResult source;
        try { source = await _tmdb.GetSupplementalMetadataAsync(new ExternalIds(tmdbId, null, item.GetType().Name), cancellationToken).ConfigureAwait(false); }
        catch (HttpRequestException) { return SupplementalOutcome.LookupFailed; }
        catch (JsonException) { return SupplementalOutcome.LookupFailed; }
        if (!string.IsNullOrWhiteSpace(source.Note)) return SupplementalOutcome.LookupFailed;
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var record = document.Items.FirstOrDefault(value => value.ItemId == item.Id);
            if (record is null) { record = new SupplementalStoredItem { ItemId = item.Id }; document.Items.Add(record); }
            record.LibraryId = libraryId; record.Name = item.Name; record.ItemType = item.GetType().Name;
            var changed = false;
            if (ratings)
            {
                var selectedCountries = (configuration.SelectedClassificationCountryCodes ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var classifications = source.Classifications.Where(value => selectedCountries.Count == 0 || selectedCountries.Contains(value.CountryCode)).ToArray();
                var primary = !string.IsNullOrWhiteSpace(configuration.PrimaryClassificationCountryCode) ? classifications.FirstOrDefault(value => string.Equals(value.CountryCode, configuration.PrimaryClassificationCountryCode, StringComparison.OrdinalIgnoreCase)) : null;
                if (configuration.AddCommunityRatings && source.VoteAverage is not null && item.CommunityRating != (float?)source.VoteAverage.Value) { item.CommunityRating = (float)source.VoteAverage.Value; changed = true; }
                if (configuration.AddAgeRatings && primary is not null && !string.Equals(item.OfficialRating, primary.Certification, StringComparison.Ordinal)) { item.OfficialRating = primary.Certification; changed = true; }
                var newRating = configuration.AddCommunityRatings ? source.VoteAverage : record.CommunityRating;
                var newVoteCount = configuration.SaveVoteCounts ? source.VoteCount : record.VoteCount;
                var newAdult = configuration.SaveAdultFlags ? source.Adult : record.Adult;
                var newClassifications = configuration.AddAgeRatings ? classifications.ToList() : record.Classifications;
                if (record.CommunityRating != newRating || record.VoteCount != newVoteCount || record.Adult != newAdult || !SameClassifications(record.Classifications, newClassifications)) changed = true;
                record.CommunityRating = newRating; record.VoteCount = newVoteCount; record.Adult = newAdult; record.OfficialRating = primary?.Certification ?? record.OfficialRating; record.Classifications = newClassifications;
                if (changed) { if (configuration.AddCommunityRatings || (configuration.AddAgeRatings && primary is not null)) await _writer.SaveAsync(item, cancellationToken).ConfigureAwait(false); _ratingsLatestChanges.Add(item.Id); }
            }
            else
            {
                var original = configuration.SaveOriginalLanguages ? source.OriginalLanguage : record.OriginalLanguage;
                var spoken = configuration.SaveSpokenLanguages ? source.SpokenLanguages.ToList() : record.SpokenLanguages;
                var translations = configuration.SaveAvailableTranslations ? source.Translations.ToList() : record.Translations;
                changed = !string.Equals(record.OriginalLanguage, original, StringComparison.OrdinalIgnoreCase) || !SameNames(record.SpokenLanguages, spoken) || !SameTranslations(record.Translations, translations);
                record.OriginalLanguage = original; record.SpokenLanguages = spoken; record.Translations = translations;
                if (changed) _languagesLatestChanges.Add(item.Id);
            }
            if (changed) await WriteUnsafeAsync(document, cancellationToken).ConfigureAwait(false);
            return changed ? SupplementalOutcome.Updated : SupplementalOutcome.Unchanged;
        }
        finally { _lock.Release(); }
    }

    private async Task<SupplementalDocument> ReadAsync(CancellationToken cancellationToken) { await _lock.WaitAsync(cancellationToken).ConfigureAwait(false); try { return await ReadUnsafeAsync(cancellationToken).ConfigureAwait(false); } finally { _lock.Release(); } }
    private async Task<SupplementalDocument> ReadUnsafeAsync(CancellationToken cancellationToken) { var path = Path.Combine(Plugin.Instance!.DataFolderPath, "supplemental-metadata.json"); if (!File.Exists(path)) return new SupplementalDocument(); await using var stream = File.OpenRead(path); return await JsonSerializer.DeserializeAsync<SupplementalDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? new SupplementalDocument(); }
    private async Task WriteUnsafeAsync(SupplementalDocument document, CancellationToken cancellationToken) { Directory.CreateDirectory(Plugin.Instance!.DataFolderPath); var path = Path.Combine(Plugin.Instance.DataFolderPath, "supplemental-metadata.json"); var temporary = path + ".tmp"; await using (var stream = File.Create(temporary)) await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false); File.Move(temporary, path, true); }
    private IEnumerable<BaseItem> GetLibraryItems(Guid libraryId) => _libraryManager.GetItemList(new InternalItemsQuery { ParentId = libraryId, Recursive = true, IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series] });
    private static bool SameNames(IEnumerable<string> left, IEnumerable<string> right) => left.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).SequenceEqual(right.OrderBy(value => value, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
    private static bool SameClassifications(IEnumerable<TmdbClassification> left, IEnumerable<TmdbClassification> right) => left.OrderBy(value => value.CountryCode).SequenceEqual(right.OrderBy(value => value.CountryCode));
    private static bool SameTranslations(IEnumerable<TmdbTranslation> left, IEnumerable<TmdbTranslation> right) => left.OrderBy(value => value.LanguageCode).ThenBy(value => value.CountryCode).SequenceEqual(right.OrderBy(value => value.LanguageCode).ThenBy(value => value.CountryCode));

    private enum SupplementalOutcome { NotConfigured, MissingId, LookupFailed, Unchanged, Updated }
    private sealed class SupplementalDocument { public List<SupplementalStoredItem> Items { get; set; } = []; }
    private sealed class SupplementalStoredItem { public Guid ItemId { get; set; } public Guid LibraryId { get; set; } public string Name { get; set; } = string.Empty; public string ItemType { get; set; } = string.Empty; public double? CommunityRating { get; set; } public int? VoteCount { get; set; } public string? OfficialRating { get; set; } public bool? Adult { get; set; } public List<TmdbClassification> Classifications { get; set; } = []; public string? OriginalLanguage { get; set; } public List<string> SpokenLanguages { get; set; } = []; public List<TmdbTranslation> Translations { get; set; } = []; }

}

/// <summary>Tracks the two dedicated supplementary metadata actions independently.</summary>
public sealed class SupplementalMetadataStateStore
{
    private readonly object _lock = new();
    private SupplementalScanProgress _ratings = new() { Message = "No ratings and classifications action is currently running." };
    private SupplementalScanProgress _languages = new() { Message = "No spoken languages and translations action is currently running." };
    public SupplementalScanProgress Get(bool ratings) { lock (_lock) { var source = ratings ? _ratings : _languages; return new SupplementalScanProgress { IsRunning = source.IsRunning, TotalItems = source.TotalItems, CompletedItems = source.CompletedItems, RecordsUpdated = source.RecordsUpdated, MissingTmdbIds = source.MissingTmdbIds, LookupFailures = source.LookupFailures, Message = source.Message }; } }
    public void Queue(bool ratings) { lock (_lock) { Set(ratings, new SupplementalScanProgress { IsRunning = true, Message = ratings ? "Ratings and classifications action queued. Jellyfin will begin it shortly." : "Spoken languages and translations action queued. Jellyfin will begin it shortly." }); } }
    public void Start(bool ratings, int total) { lock (_lock) Set(ratings, new SupplementalScanProgress { IsRunning = true, TotalItems = total, Message = ratings ? "Loading selected-library ratings and classifications…" : "Loading selected-library spoken languages and translations…" }); }
    internal void Record(bool ratings, object outcome) { lock (_lock) { var progress = ratings ? _ratings : _languages; progress.CompletedItems++; if (outcome.ToString()?.Contains("Updated", StringComparison.Ordinal) == true) progress.RecordsUpdated++; if (outcome.ToString()?.Contains("MissingId", StringComparison.Ordinal) == true) progress.MissingTmdbIds++; if (outcome.ToString()?.Contains("LookupFailed", StringComparison.Ordinal) == true) progress.LookupFailures++; } }
    public void Complete(bool ratings, string? error = null) { lock (_lock) { var progress = ratings ? _ratings : _languages; progress.IsRunning = false; progress.Message = error ?? (ratings ? $"Ratings and classifications action complete — checked {progress.CompletedItems} of {progress.TotalItems} media items; updated {progress.RecordsUpdated} record(s). {progress.MissingTmdbIds} had no TMDb ID and {progress.LookupFailures} TMDb lookup failure(s)." : $"Spoken languages and translations action complete — checked {progress.CompletedItems} of {progress.TotalItems} media items; updated {progress.RecordsUpdated} record(s). {progress.MissingTmdbIds} had no TMDb ID and {progress.LookupFailures} TMDb lookup failure(s)."); } }
    private void Set(bool ratings, SupplementalScanProgress value) { if (ratings) _ratings = value; else _languages = value; }
}
