# Jellyfin 10.11.11 Compatibility Audit

## Scope and method

This audit covers the complete current plugin: every dashboard control and its
custom endpoint, every Jellyfin extension point, every metadata write, and
every background operation. It uses the official Jellyfin `v10.11.11` source
as the version-specific authority, alongside the exact `Jellyfin.Controller`
and `Jellyfin.Model` `10.11.11` packages used by the project.

`Supported` means the implementation uses a public 10.11.11 extension point or
API with matching semantics. `Live check required` means the code path is
supported but must still be exercised on a real server. This document never
turns a source-level finding into a claim that a live test has passed.

Primary references:

- [BasePlugin<TConfiguration>](https://github.com/jellyfin/jellyfin/blob/v10.11.11/MediaBrowser.Common/Plugins/BasePluginOfT.cs)
- [Plugin web pages](https://github.com/jellyfin/jellyfin/blob/v10.11.11/MediaBrowser.Model/Plugins/PluginPageInfo.cs)
- [Plugin service registration](https://github.com/jellyfin/jellyfin/blob/v10.11.11/MediaBrowser.Controller/Plugins/IPluginServiceRegistrator.cs)
- [Library manager](https://github.com/jellyfin/jellyfin/blob/v10.11.11/MediaBrowser.Controller/Library/ILibraryManager.cs)
- [Library metadata-update implementation](https://github.com/jellyfin/jellyfin/blob/v10.11.11/Emby.Server.Implementations/Library/LibraryManager.cs)
- [Scheduled tasks](https://github.com/jellyfin/jellyfin/blob/v10.11.11/MediaBrowser.Model/Tasks/IScheduledTask.cs)
- [Scheduled-task trigger updates](https://github.com/jellyfin/jellyfin/blob/v10.11.11/Jellyfin.Api/Controllers/ScheduledTasksController.cs)
- [Post-library-scan tasks](https://github.com/jellyfin/jellyfin/blob/v10.11.11/MediaBrowser.Controller/Library/ILibraryPostScanTask.cs)

## Plugin and persistence contract

| Surface | Checked implementation | Result |
| --- | --- | --- |
| Runtime ABI | The project targets `net9.0` and references `Jellyfin.Controller` and `Jellyfin.Model` `10.11.11`. | Supported; build check required for every release. |
| Identity and configuration | `Plugin` derives from `BasePlugin<PluginConfiguration>` and uses its stable plugin ID. Every update creates a private configuration copy, calls Jellyfin's `UpdateConfiguration`, and therefore saves through the native configuration path. | Supported. The clone prevents a stale dashboard tab or quota update from replacing unrelated settings. |
| Dashboard/sidebar page | `IHasWebPages`, embedded `configPage.html`, and `PluginPageInfo.EnableInMainMenu` provide the Dashboard-only entry. | Supported; live visual check required. |
| Administrator boundary | Every custom controller route uses Jellyfin's `RequiresElevation` policy. | Supported; live authorization check required. |
| Dashboard HTTP calls | The page uses Jellyfin Web's `ApiClient.getJSON`, `ApiClient.ajax`, and authenticated `getUrl` pattern for logo images. It does not rely on private Dashboard configuration endpoints. | Supported; live browser check required. |
| Settings saves | Main, API, Provider/Network, individual Provider, individual Network, Genres/Keywords, Cast/Crew, more like this, Production, Ratings, Languages, and Scheduled Tasks have scoped request models and only mutate their own configuration fields. A Main Settings save preserves API/quota fields unless they were edited; an API save reloads source catalogs only when a credential changed. | Supported; live persistence/reload check required. |
| Credential removal | A blank saved TMDb or Watchmode field now disables that source, matching the dashboard copy and the documented source behavior. | Supported; live check required. |
| Retired Tag Destination(s) fields | The settings page, request models, and persisted configuration no longer use the two destination fields. A pre-change configuration must still be opened on a real server to confirm Jellyfin ignores those retired XML elements while retaining every remaining setting. | Live check required. |

## Dashboard controls and custom endpoints

| Dashboard area and controls | Jellyfin/API path checked | Result |
| --- | --- | --- |
| Main Settings: selected libraries, API settings, logo settings, new-media toggle, scheduled settings, main save | `GetVirtualFolders(true)`, custom authorized settings routes, native configuration update. | Supported; all save/reload combinations require live checks. |
| Backup Settings: create, list, restore, delete, undo | Plugin data folder for JSON snapshots; restored tags use the same native metadata update as scans. | Supported; restore/undo require live safety checks. |
| Network and Provider Settings: regions, tag-kind toggles, TV-network app mode, picker search, Select All/None, independent saves, sync actions | Custom routes normalize explicit selections; source catalogs remain transient; tag sync uses the native item update. | Supported; catalog coverage and large-list behavior require live checks. |
| Genres and Keywords: picker search, Select All/None, save, sync genres, remove plugin keywords | Custom routes plus the same selected-library metadata writer. | Supported; source results and tag effects require live checks. |
| Collections: scan matches, Select All/None, add collection tags | Selected-library Movie query plus a revalidated direct TMDb collection lookup before each write. | Supported Jellyfin integration; TMDb coverage requires live checks. |
| Cast and Crew: secondary tab, scoped save, cast/crew choices, missing-photo action, cleanup | `ILibraryManager.GetPeople`, `UpdatePeopleAsync`, `GetPerson`, person `HasImage`/`GetImagePath`/`DeleteImageAsync`, and `IProviderManager.SaveImage` are public 10.11.11 APIs. The dedicated photo action is a registered `IScheduledTask` queued through `ITaskManager`. | Supported API usage; preservation, photo ownership, and task progress require live checks. |
| more like this: secondary tab, scoped save, load, update, record-only removal, paged relationship overview, poster cache | Authorized custom settings/data routes and plugin-owned `DataFolderPath` JSON/cache files. Explicit Load and Update routes queue dedicated relationship work through the same public `IScheduledTask`/`ITaskManager` path as the plugin's other long-running Dashboard actions. Lightweight count and bounded page routes avoid rendering every relationship card at once. This data is intentionally separate from Jellyfin metadata and does not use a private Jellyfin recommendation API. | Supported Jellyfin integration; TMDb result/caching/rendering behavior requires live checks. |
| Production Companies and Countries: secondary tab, scoped save, country picker, dedicated load, native metadata update, logo cache, cleanup, overview, direct edit | TMDb title details and configuration-country data feed native `BaseItem.Studios` and `BaseItem.ProductionLocations`; every changed item uses `ILibraryManager.UpdateItemAsync(..., MetadataEdit, ...)`. The dedicated Load action queues a registered `IScheduledTask` through `ITaskManager`. Plugin-owned provenance remains in its data folder only to constrain cleanup. | Supported public 10.11.11 metadata, task, and update paths; live checks required for NFO representation, metadata preservation, task progress, and cleanup ownership. |
| Ratings Settings and Spoken Languages and Translations Settings: scoped saves, dedicated loads, progress, and collapsible selected-library overview | TMDb title details/release-date or TV-content-rating/translation responses are consumed only by administrator-authorized plugin routes. Native `BaseItem.CommunityRating` and the intentionally selected primary `BaseItem.OfficialRating` update through `ILibraryManager.UpdateItemAsync(..., MetadataEdit, ...)`; vote counts, all-country certifications, adult flag, spoken languages, and translations are retained in plugin-owned data because Jellyfin's supported base item model has no corresponding multi-value fields. Both dedicated actions queue registered `IScheduledTask` implementations through `ITaskManager`. | Supported public 10.11.11 metadata, task, and update paths; live checks required for source coverage, native/NFO representation, progress, and dashboard overview behavior. |
| Provider/Network and Genre/Keyword Library Overviews: filters, expand/collapse, manual edits, save changes | Selected-library `GetItemList` queries expose both `BaseItem.Tags` and native `BaseItem.Genres`. The Genre/Keyword overview combines native genres with prefixed Genre tags for display/filtering, while its edit payload remains limited to prefixed Genre and Keyword tags. | Supported source path; native-genre rendering and read-only preservation require live checks. |
| Unknown Providers and Networks: list, See Items, official name, logo upload | Selected-library queries, plugin configuration mappings, plugin-data logo cache, and a browser-native authenticated multipart request for PNG, JPEG, or SVG files. | Supported source path; upload/content-type, cache refresh, and display require live checks. |
| Logo Settings: load all, load selected providers, status/progress, delete all, delete selected | Plugin-owned cache and source downloads; cached images use authenticated custom routes. | Supported Jellyfin integration; source availability and browser rendering require live checks. |
| Scan: selected-library summary, scan all, stop, progress/ETA, last-scan additions/removals, scan undo, duplicate backup/scheduled controls | `ITaskManager` queues/cancels a registered `IScheduledTask`; the latest-scan delta and colors are plugin-owned presentation state and do not replace Jellyfin task progress or metadata. | Supported; task startup, cancellation, ETA accuracy, and last-scan display/edit behavior require live checks. |

## Metadata, NFO, and background operations

| Operation | Checked implementation | Result |
| --- | --- | --- |
| Every tag write | Scans, selection sync, collection apply, manual edits, backup restore, and undo call `ILibraryManager.UpdateItemAsync(..., MetadataEdit, ...)` once. | Supported. |
| Cast and crew enrichment | Normal scans read the item’s current Jellyfin people list, append only opted-in missing TMDb cast or selected-job crew records, then call `UpdatePeopleAsync`. The dedicated photo action reads only selected-library people and writes a missing person primary image through `IProviderManager.SaveImage`. | Supported public 10.11.11 APIs; live checks required for existing-NFO preservation, person-image sharing, and cleanup safety. |
| more like this relationship storage | Normal scans retain TMDb direct title-to-title result records under the plugin `DataFolderPath`. They do not write tags, NFO fields, media files, Jellyfin images, user watch history, or Jellyfin recommendation state. Optional poster files remain in a bounded plugin-owned cache served only by an authorized custom route. | Supported; live checks required for source result coverage, cache limits, and dashboard rendering. |
| NFO behavior | The plugin no longer exposes Tag Destination(s) or calls `IProviderManager.SaveMetadataAsync` itself. Jellyfin's normal metadata-update path invokes configured metadata savers, so each library's own metadata settings govern NFO output. | Corrected to supported native behavior; live NFO-on/off checks required. |
| Selected-library scope | Queries use selected virtual-folder IDs, recursive `InternalItemsQuery`, and only Movie/Series kinds. Empty selection is never treated as all libraries. | Supported; live multi-library checks required. |
| Backup files | Snapshots live below the plugin `DataFolderPath`, are serialized under a file lock, and do not alter media until an explicit restore/undo. | Supported; disk-permission and restore checks required. |
| Manual scan task | `ManualScanTask` is a registered `IScheduledTask`; dashboard requests are queued through `ITaskManager`, not detached work. | Supported. |
| Scheduled refresh | `RefreshAvailabilityTask` implements `IScheduledTask`. Saving scheduled settings explicitly replaces the registered worker's triggers, because Jellyfin otherwise reuses persisted trigger settings rather than re-reading defaults. | Corrected to supported live-task behavior; live schedule check required. |
| New incoming media | Registered `ILibraryPostScanTask` runs only when enabled and does not break a normal Jellyfin library scan when setup is incomplete. | Supported; live hook check required. |
| Scan status | A singleton state store reports active title, completed count, totals, tag additions, and safe numeric ETA to the dashboard. | Supported plugin behavior; live accuracy check required. |
| Logo-download background work | The administrator-started, plugin-owned logo cache loader runs separately from scans and exposes only plugin cache/progress state. It does not alter Jellyfin media metadata or task state. | Live lifecycle check required; Jellyfin has no separate public logo-cache extension point to substitute here. |

## External-source boundary

TMDb and Watchmode are not Jellyfin APIs. Their title matching, catalog coverage,
quota limits, and logos are reviewed against their own documentation and remain
separate from this Jellyfin compatibility result.

The Jellyfin-facing safeguards are:

- failed source calls and missing external IDs preserve existing managed tags;
- Watchmode remains the quota-tracked fallback where TMDb does not provide the
  requested availability result;
- TMDb calls use the shared request gate and back off on HTTP 429;
- external logos are optional and cannot prevent media-tag writes.

## Findings that require a live server

The remaining live tests are deliberately tracked in
[goal-testing.txt](goal-testing.txt). They include installation/update
persistence, every scoped save control, sidebar placement, source credentials,
library enumeration, NFO behavior under two library configurations, all scan
paths, scheduled execution, post-scan incoming-media behavior, logo rendering,
backup restore, and manual editing.

No item is marked live-passed here until it has been tested on a Jellyfin
10.11.11 server.
