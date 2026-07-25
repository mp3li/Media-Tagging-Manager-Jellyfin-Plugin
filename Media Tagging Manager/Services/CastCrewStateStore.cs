using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Keeps dedicated cast-and-crew photo scan progress separate from Jellyfin's scheduled-task UI.</summary>
public sealed class CastCrewStateStore
{
    private readonly object _lock = new();
    private CastCrewPhotoProgress _progress = new();

    /// <summary>Gets a stable snapshot for dashboard polling.</summary>
    public CastCrewPhotoProgress GetProgress()
    {
        lock (_lock)
        {
            return new CastCrewPhotoProgress
            {
                IsRunning = _progress.IsRunning,
                TotalItems = _progress.TotalItems,
                CompletedItems = _progress.CompletedItems,
                MissingPhotoCount = _progress.MissingPhotoCount,
                TmdbPhotoAvailableCount = _progress.TmdbPhotoAvailableCount,
                PhotosAdded = _progress.PhotosAdded,
                EstimatedBytes = _progress.EstimatedBytes,
                Message = _progress.Message
            };
        }
    }

    /// <summary>Shows that a dashboard request is waiting for Jellyfin's scheduled-task worker.</summary>
    public void Queue()
    {
        lock (_lock)
        {
            _progress = new CastCrewPhotoProgress
            {
                IsRunning = true,
                Message = "Cast and crew photo scan queued. Jellyfin will begin it shortly."
            };
        }
    }

    /// <summary>Starts a new dedicated person-photo scan.</summary>
    public void Start(int totalItems)
    {
        lock (_lock)
        {
            _progress = new CastCrewPhotoProgress
            {
                IsRunning = true,
                TotalItems = totalItems,
                Message = "Scanning selected-library cast and crew for missing photos…"
            };
        }
    }

    /// <summary>Records one inspected media item and its people-photo outcome.</summary>
    public void RecordItem(int missingPhotos, int tmdbPhotosAvailable, int photosAdded, long bytes)
    {
        lock (_lock)
        {
            _progress.CompletedItems++;
            _progress.MissingPhotoCount += Math.Max(0, missingPhotos);
            _progress.TmdbPhotoAvailableCount += Math.Max(0, tmdbPhotosAvailable);
            _progress.PhotosAdded += Math.Max(0, photosAdded);
            _progress.EstimatedBytes += Math.Max(0, bytes);
        }
    }

    /// <summary>Completes the dedicated people-photo scan with a useful dashboard message.</summary>
    public void Complete(string? warning = null)
    {
        lock (_lock)
        {
            _progress.IsRunning = false;
            _progress.Message = string.IsNullOrWhiteSpace(warning)
                ? $"Cast and crew photo scan complete — checked {_progress.CompletedItems} of {_progress.TotalItems} media items; found {_progress.MissingPhotoCount} missing photos, TMDb supplied {_progress.TmdbPhotoAvailableCount}, and saved {_progress.PhotosAdded} ({FormatBytes(_progress.EstimatedBytes)})."
                : warning;
        }
    }

    private static string FormatBytes(long bytes) => bytes < 1024 * 1024
        ? $"{bytes / 1024d:0.0} KB"
        : $"{bytes / 1024d / 1024d:0.0} MB";
}
