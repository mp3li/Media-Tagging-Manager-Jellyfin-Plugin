using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Keeps dedicated More Like This progress separate from normal tag scan state.</summary>
public sealed class MoreLikeThisStateStore
{
    private readonly object _lock = new();
    private MoreLikeThisScanProgress _progress = new();

    /// <summary>Gets a stable progress snapshot for dashboard polling.</summary>
    public MoreLikeThisScanProgress GetProgress()
    {
        lock (_lock)
        {
            return new MoreLikeThisScanProgress
            {
                IsRunning = _progress.IsRunning,
                TotalItems = _progress.TotalItems,
                CompletedItems = _progress.CompletedItems,
                RecordsSaved = _progress.RecordsSaved,
                RecommendationsSaved = _progress.RecommendationsSaved,
                SimilarTitlesSaved = _progress.SimilarTitlesSaved,
                Message = _progress.Message
            };
        }
    }

    /// <summary>Records a queued Jellyfin task before its worker begins.</summary>
    public void Queue(string action)
    {
        lock (_lock)
        {
            _progress = new MoreLikeThisScanProgress
            {
                IsRunning = true,
                Message = $"{action} queued. Jellyfin will begin it shortly."
            };
        }
    }

    /// <summary>Begins a selected-library relationship action.</summary>
    public void Start(int totalItems, string action)
    {
        lock (_lock)
        {
            _progress = new MoreLikeThisScanProgress
            {
                IsRunning = true,
                TotalItems = totalItems,
                Message = $"{action} selected-library recommendations and similar titles…"
            };
        }
    }

    /// <summary>Records one inspected title's current relationship result.</summary>
    public void RecordItem(int recommendations, int similarTitles, bool saved)
    {
        lock (_lock)
        {
            _progress.CompletedItems++;
            _progress.RecommendationsSaved += Math.Max(0, recommendations);
            _progress.SimilarTitlesSaved += Math.Max(0, similarTitles);
            if (saved)
            {
                _progress.RecordsSaved++;
            }
        }
    }

    /// <summary>Marks the dedicated relationship action complete with a useful count summary.</summary>
    public void Complete(string? error = null)
    {
        lock (_lock)
        {
            _progress.IsRunning = false;
            _progress.Message = string.IsNullOrWhiteSpace(error)
                ? $"Recommendations and Similar Titles action complete — checked {_progress.CompletedItems} of {_progress.TotalItems} media items; saved {_progress.RecommendationsSaved} recommendations and {_progress.SimilarTitlesSaved} similar titles across {_progress.RecordsSaved} updated media record(s)."
                : error;
        }
    }
}
