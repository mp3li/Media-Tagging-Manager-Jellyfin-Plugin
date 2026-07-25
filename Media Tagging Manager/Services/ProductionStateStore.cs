using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Keeps dedicated production-metadata action progress separate from normal tag scan state.</summary>
public sealed class ProductionStateStore
{
    private readonly object _lock = new();
    private ProductionScanProgress _progress = new();

    /// <summary>Gets a stable progress snapshot for dashboard polling.</summary>
    public ProductionScanProgress GetProgress()
    {
        lock (_lock)
        {
            return new ProductionScanProgress
            {
                IsRunning = _progress.IsRunning,
                TotalItems = _progress.TotalItems,
                CompletedItems = _progress.CompletedItems,
                CompaniesAdded = _progress.CompaniesAdded,
                CountriesAdded = _progress.CountriesAdded,
                ItemsChanged = _progress.ItemsChanged,
                Message = _progress.Message
            };
        }
    }

    /// <summary>Records a queued Jellyfin task before its worker begins.</summary>
    public void Queue()
    {
        lock (_lock)
        {
            _progress = new ProductionScanProgress { IsRunning = true, Message = "Production companies and countries action queued. Jellyfin will begin it shortly." };
        }
    }

    /// <summary>Begins the selected-library action.</summary>
    public void Start(int totalItems)
    {
        lock (_lock)
        {
            _progress = new ProductionScanProgress { IsRunning = true, TotalItems = totalItems, Message = "Loading selected-library production companies and countries…" };
        }
    }

    /// <summary>Records one inspected item result.</summary>
    public void Record(ProductionOperationResult result)
    {
        lock (_lock)
        {
            _progress.CompletedItems++;
            _progress.CompaniesAdded += Math.Max(0, result.CompaniesAdded);
            _progress.CountriesAdded += Math.Max(0, result.CountriesAdded);
            _progress.ItemsChanged += Math.Max(0, result.ItemsChanged);
        }
    }

    /// <summary>Marks the action complete with an administrator-facing result.</summary>
    public void Complete(string? error = null)
    {
        lock (_lock)
        {
            _progress.IsRunning = false;
            _progress.Message = string.IsNullOrWhiteSpace(error)
                ? $"Production companies and countries action complete — checked {_progress.CompletedItems} of {_progress.TotalItems} media items; added {_progress.CompaniesAdded} production compan{(_progress.CompaniesAdded == 1 ? "y" : "ies")} and {_progress.CountriesAdded} production countr{(_progress.CountriesAdded == 1 ? "y" : "ies")} across {_progress.ItemsChanged} changed media item{(_progress.ItemsChanged == 1 ? string.Empty : "s")}."
                : error;
        }
    }
}
