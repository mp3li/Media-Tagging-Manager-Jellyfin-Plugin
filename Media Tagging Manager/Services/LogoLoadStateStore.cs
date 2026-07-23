using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Tracks one administrator-requested background logo-cache operation.</summary>
public sealed class LogoLoadStateStore
{
    private readonly object _sync = new();
    private LogoLoadProgress _progress = new();

    /// <summary>Starts a new logo-load operation when another one is not active.</summary>
    public bool TryStart()
    {
        lock (_sync)
        {
            if (_progress.IsRunning)
            {
                return false;
            }

            _progress = new LogoLoadProgress
            {
                IsRunning = true,
                Message = "Preparing source-supplied logos…"
            };
            return true;
        }
    }

    /// <summary>Sets the number of source-supplied logos selected for processing.</summary>
    public void SetTotal(int total)
    {
        lock (_sync)
        {
            _progress.Total = total;
            _progress.Message = total == 0 ? "No source-supplied logos were available to load." : $"Loading 0 of {total} logos…";
        }
    }

    /// <summary>Records one completed source-logo processing attempt.</summary>
    public void Report(int completed)
    {
        lock (_sync)
        {
            _progress.Completed = completed;
            _progress.Message = $"Loading {completed} of {_progress.Total} logos…";
        }
    }

    /// <summary>Completes the active logo-load operation.</summary>
    public void Complete(string? message = null)
    {
        lock (_sync)
        {
            _progress.IsRunning = false;
            _progress.Message = message ?? (_progress.Total == 0 ? "No source-supplied logos were available to load." : $"Processed {_progress.Completed} of {_progress.Total} logos.");
        }
    }

    /// <summary>Returns a stable snapshot for the dashboard.</summary>
    public LogoLoadProgress GetProgress()
    {
        lock (_sync)
        {
            return new LogoLoadProgress
            {
                IsRunning = _progress.IsRunning,
                Total = _progress.Total,
                Completed = _progress.Completed,
                Message = _progress.Message
            };
        }
    }
}
