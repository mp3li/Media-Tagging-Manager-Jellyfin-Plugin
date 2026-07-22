# Jellyfin 10.11.11 Compatibility Audit

## Scope and method

This is a source-level audit of the complete plugin as built against Jellyfin
`10.11.11`. It compares the implementation with Jellyfin's official plugin
template, the `10.11.11` server source, and the `10.11.11` `Jellyfin.Controller`
and `Jellyfin.Model` API packages installed by the project build. It does not
claim that a compiled plugin has already succeeded on a particular server; the
live-server checks remain recorded in [goal-testing.txt](goal-testing.txt).

Primary Jellyfin references:

- [Official plugin template](https://github.com/jellyfin/jellyfin-plugin-template)
- [10.11.11 LibraryStructureController](https://github.com/jellyfin/jellyfin/blob/v10.11.11/Jellyfin.Api/Controllers/LibraryStructureController.cs)
- [10.11.11 LibraryController](https://github.com/jellyfin/jellyfin/blob/v10.11.11/Jellyfin.Api/Controllers/LibraryController.cs)
- [10.11.11 task manager implementation](https://github.com/jellyfin/jellyfin/blob/v10.11.11/Emby.Server.Implementations/ScheduledTasks/TaskManager.cs)

## Compatibility result

| Area | Implementation | 10.11.11 basis | Result |
| --- | --- | --- | --- |
| Runtime target and package ABI | `net9.0`; `Jellyfin.Controller` and `Jellyfin.Model` are both `10.11.11`, with runtime assets excluded. | The official template requires the package version to match the installed server to avoid `NotSupported`. | Supported |
| Plugin identity and persisted configuration | `Plugin` derives from `BasePlugin<PluginConfiguration>` and the configuration derives from `BasePluginConfiguration`. | The official template documents these as the supported plugin/configuration bases. | Supported |
| Dashboard page | `Plugin` implements `IHasWebPages`; `configPage.html` is embedded and exposed as `PluginPageInfo`. | `IHasWebPages` and `PluginPageInfo.EmbeddedResourcePath` are part of `Jellyfin.Model` 10.11.11. | Supported |
| Dashboard-only sidebar entry | `EnableInMainMenu = true` is set on the same `PluginPageInfo`. | `PluginPageInfo.EnableInMainMenu` is the 10.11.11 menu property. File Transformation uses this exact property for its Dashboard plugin entry. No invented `MenuSection` value is used. | Supported; needs one live visual check |
| Administrator access | Every custom controller endpoint uses Jellyfin's `RequiresElevation` policy. | Jellyfin's own 10.11.11 `PluginsController` uses that policy for administrator plugin operations. | Supported |
| Settings endpoint | The page reads and writes the plugin's `PluginConfiguration` through its administrator-only controller. | Jellyfin supports custom `ControllerBase` REST endpoints and `BasePlugin.UpdateConfiguration`. | Supported |
| Library picker | The settings endpoint gets libraries with `ILibraryManager.GetVirtualFolders(true)`. | That is the exact call in Jellyfin 10.11.11's `LibraryStructureController`. | Supported; needs live response check |
| Selected-library scans | The scanner uses `ILibraryManager.GetItemList` with a parent ID, recursive search, and Movie/Series item kinds. | `ILibraryManager` is the official plugin interface for direct library access. | Supported |
| Tag writes | The scanner and backup restore use `ILibraryManager.UpdateItemAsync(..., ItemUpdateType.MetadataEdit, ...)`. | `UpdateItemAsync` and metadata-edit update reason are exposed by the 10.11.11 controller API. | Supported |
| Manual tag editing | The scanner now rejects items outside selected libraries and items other than Movies or Series before backing up or writing. | This enforces the plugin's selected-library boundary on top of the supported item APIs. | Supported |
| Full automatic refresh | `RefreshAvailabilityTask` implements `IScheduledTask`; interval triggers are returned only when enabled. | The official template identifies `IScheduledTask` as the supported scheduled-work extension point. | Supported |
| Scan buttons | Dashboard requests are placed in `ManualScanRequestQueue` and started through `ITaskManager.QueueScheduledTask<ManualScanTask>()`; no `Task.Run` remains. | `ITaskManager.QueueScheduledTask<T>` is present in 10.11.11 and its source locates a registered `IScheduledTask` by concrete type before queueing it. | Supported |
| New incoming media | `NewMediaPostScanTask` implements `ILibraryPostScanTask`, registers through the plugin service registrator, and only scans after the setting is enabled. | The official template identifies `ILibraryPostScanTask` as the supported post-library-scan extension point. | Supported; needs live hook check |
| Service registration | `ServiceRegistrator` implements `IPluginServiceRegistrator` with a parameterless constructor and registers plugin services via `IServiceCollection`. | The 10.11.11 API documents that interface specifically for plugin DI registration. | Supported |
| Scan status and remaining estimate | An in-memory singleton state store reports current item, completion, and elapsed-rate estimate to the page. | This is ordinary plugin service logic; Jellyfin supplies the task `IProgress<double>` channel, while the richer estimate is plugin-owned. | Supported design; estimate needs live accuracy check |
| Backups and undo | Complete tag snapshots are JSON files under the plugin `DataFolderPath`; restore uses the supported library update API. | `BasePlugin.DataFolderPath` and `UpdateItemAsync` are supported APIs. | Supported; restore is intentionally destructive and requires confirmation |
| Library overview and filters | Overview re-reads the selected libraries through `ILibraryManager.GetItemList`; filters are server-side. | Same supported library query API as scanning. | Supported |
| Browser page behavior | The embedded page calls only this plugin's custom authorized endpoints. JSON reads use `ApiClient.getJSON`, the exact Jellyfin Web 10.11.11 helper that parses JSON GET responses; writes use `ApiClient.ajax`. It no longer depends on the dashboard's internal `Library/VirtualFolders` or plugin-configuration endpoint. | The custom-controller approach is documented by the official plugin template, and `jellyfin-web` 10.11.11's `src/utils/fetch.js` defines the `getJSON`/JSON parsing behavior. | Supported; needs live check |

## External data-source audit

These are not Jellyfin APIs, so Jellyfin documentation cannot certify them. They
were checked against the providers' own public documentation instead.

| Source | Current implementation | Source rule applied | Result |
| --- | --- | --- | --- |
| TMDb | Uses a Bearer API Read Access Token, movie/TV watch-provider endpoints, TV details for `networks`, and the official available-regions endpoint for up to three country choices. | TMDb documents watch-provider availability as country-specific and requires JustWatch attribution. The dashboard includes that attribution. | Supported, subject to each user's TMDb API Read Access Token and data coverage |
| Watchmode | Uses the documented `X-API-Key` header, IMDb ID accepted by its title-sources endpoint, selected regions, and account-quota usage header. It is queried only when TMDb has no provider result. | Watchmode documents the header as preferred, its title-sources endpoint as accepting IMDb IDs, `regions` as the regional filter, and the quota headers. | Supported, subject to the user's plan/quota |

External references:

- [TMDb Watch Providers](https://developer.themoviedb.org/reference/movie-watch-providers)
- [TMDb TV Series Details](https://developer.themoviedb.org/reference/tv-series-details)
- [Watchmode API documentation](https://api.watchmode.com/docs)

## Safety boundaries verified in code

- No API key, server address, backup, build output, or `.DS_Store` file is
  committed by the repository's ignore rules.
- API keys are supplied by the administrator in Jellyfin settings, never from a
  repository file or release manifest. Jellyfin plugin configuration is not an
  encrypted secret vault; a server administrator who can read plugin settings
  can read those keys.
- Source failures, missing TMDb/IMDb IDs, and non-success custom-source
  responses preserve previously managed tags rather than clearing them.
- Only `Provider: ` and `Network: ` tags are owned and replaced by this plugin.
  Other Jellyfin tags remain untouched during normal scans.
- Every automatic or manual write creates a complete selected-library backup
  first. A restore replaces every tag list captured by that backup, including
  tags the plugin did not create; the page warns before restoration.
- Manual edits are restricted to selected libraries and Movie/Series items.

## What an audit cannot prove without the test server

The audit establishes that the extension points and APIs used are present and
matched to 10.11.11. It cannot prove network reachability, the server's actual
library response, permissions, source credentials, or dashboard rendering from
this computer. The next live build must specifically verify the library list,
the new Dashboard sidebar entry, task execution/progress, an incoming-media
hook, provider queries, manual edits, backup/restore, and failure preservation.
Those results belong only in [goal-testing.txt](goal-testing.txt), not in the
project goals.
