# Changelog

All notable changes to Media Tagging Manager Jellyfin Plugin by mp3li are documented in this file.

The format follows the spirit of [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), with release entries added only when a real packaged release exists.

## [Unreleased]

## [0.1.0.30-test] - 2026-07-23

### Fixed

- Saving an empty Provider or Network selection now prevents future additions
  without allowing a later scan to remove existing tags. Only the explicit
  corresponding Sync action can remove unselected Provider or Network tags.
- Library Overview now reads the saved selected-library configuration directly,
  so it remains populated when its Main Settings checkbox controls are hidden.

### Test-release notes

- This supersedes `0.1.0.29-test` and remains a public test build, not a
  stable release.

## [0.1.0.29-test] - 2026-07-23

### Fixed

- Corrected the Unknown Providers and Networks **See Items** dialog's hidden
  state so it does not block the View and Edit Tags tab on page load and its
  Close button works normally.

### Test-release notes

- This supersedes `0.1.0.28-test` and remains a public test build, not a
  stable release.

## [0.1.0.28-test] - 2026-07-23

### Added

- Added **Genres and Keywords Settings** with a searchable, scrollable TMDb
  genre picker, independent Select All/Select None controls, a saved genre
  allow-list, and **Sync with Only Selected Genres**.
- Added opt-in TMDb keyword tagging. Keywords are written as `Keyword:` tags
  only when enabled before a scan, and can be removed with the backup-protected
  **Remove Keywords Added by This Plugin** action without an API request.
- Added **Collections Tags Settings**. It scans selected libraries for direct
  TMDb movie collection matches, groups reviewable results by library, and adds
  only administrator-selected `Collection:` tags after a backup.
- Expanded **View and Edit Tags** so Provider, Network, Genre, Keyword, and
  Collection tags are separate columns and separately editable.
- Added **See Items** for Unknown Providers and Networks, opening the exact
  selected-library items that carry the chosen unknown tag.

### Changed

- Scheduled outdated-availability replacement remains limited to Provider and
  Network tags. Genre, Keyword, and Collection tags use their dedicated
  controls and are not removed by that availability setting.
- Logo preferences now save with **Save Main Settings**; disabling logo use
  also stops dashboard logo requests. The redundant standalone save button was
  removed.

## [0.1.0.27-test] - 2026-07-23

### Added

- Provider and Network picker rows and View Tags now use a server-side cache
  of source-supplied logos. The cache keeps one image per normalized tag name,
  not one image per media item, and exposes those cached images to compatible
  local plugins.
- Added **Logo Settings** to stop logo use/saving and explicitly delete all
  cached or manually uploaded logos without changing media tags.
- Added **Unknown Providers and Networks**, limited to unknown prefixed
  `Provider:`/`Network:` tags. It supports persistent official-name mappings
  and a single manually uploaded PNG, JPEG, or SVG logo per mapping.
- Added a reserved, empty **Genre Settings** dashboard tab.
- Added a one-click **Clear Filters** action and a collapsed-by-default
  Library Overview in **View and Edit Tags**.

### Changed

- Clarified that TMDb and Watchmode can each return an actual title-level
  Network tag, and that the TV Network Streaming Apps **Both** mode requires
  selecting the app under Providers and its network under Networks.
- Reorganized dashboard settings into **Main Settings** and **Network and
  Provider Settings**, each with a dedicated save action. Scheduled Tasks
  Settings are now also available on Scan with their own save action and stay
  synchronized with Main Settings.
- Renamed **View Tags** to **View and Edit Tags**.

### Test-release notes

- This supersedes `0.1.0.26-test` and remains a public test build, not a stable
  release.

## [0.1.0.26-test] - 2026-07-22

### Added

- Added independent **Save Provider Selections** and **Save Network Selections**
  buttons. Each persists only its own future-scan allow-list and does not scan
  media, create a backup, or modify existing tags.

### Test-release notes

- This supersedes `0.1.0.25-test` and remains a public test build, not a stable
  release.

## [0.1.0.25-test] - 2026-07-22

### Added

- Added **TV Network Streaming Apps** with three administrator-selected modes:
  Network only, Streaming app only, and Both. The plugin never invents a
  Network tag from a streaming-app name.
- Watchmode fallback title details now use Watchmode's actual per-title
  `network_names` separately from its current streaming sources.
- Dashboard-initiated full scans now require an existing tag backup.

### Changed

- Removed the premature automatic provider-family grouping introduced in
  `0.1.0.24-test`. Exact provider variants remain separate unless an
  administrator selectively manages their tags.
- Complete Provider and Network selection lists remain available before scans.

### Test-release notes

- This supersedes `0.1.0.24-test` and remains a public test build, not a stable
  release.

## [0.1.0.24-test] - 2026-07-22

### Added

- Added the optional **Group different types of the same provider** preference.
  It is off by default and groups only documented Netflix, Apple TV, and
  Amazon/Prime variants when enabled; exact source distinctions remain
  available when disabled.

### Fixed

- Provider and network selection sync failures now return and display a safe,
  specific server explanation instead of only “An unknown error occurred.”
- Saving Main Settings now persists the current provider and network checkbox
  selections for future scans.

### Test-release notes

- This supersedes `0.1.0.23-test` and remains a public test build, not a stable
  release.

## [0.1.0.23-test] - 2026-07-22

### Changed

- Removed source-status messages from above the provider and network searches.
- Combined only explicit same-service provider spelling aliases: Apple TV Plus,
  Disney Plus/Disney +, and Discovery Plus/Discovery + now use canonical names.
  Separate storefronts, subscriptions, plan tiers, and profile variants remain
  distinct choices.

### Test-release notes

- This supersedes `0.1.0.22-test` and remains a public test build, not a stable
  release.

## [0.1.0.22-test] - 2026-07-22

### Added

- Added a shared TMDb request gate capped at 35 requests per second.
- TMDb HTTP 429 responses now trigger a temporary plugin-wide cooldown and up
  to two safe read retries, honoring a `Retry-After` response value when one is
  supplied.

### Test-release notes

- This supersedes `0.1.0.21-test` and remains a public test build, not a stable
  release.

## [0.1.0.21-test] - 2026-07-22

### Changed

- Replaced calendar-month Watchmode quota tracking with 30-day cycles anchored
  to the administrator-entered **Quota Resets On** date.
- Added the requested Watchmode reset-date guidance and a visible active-cycle
  status. Provider/network reference-catalog requests now use the same quota
  guard as title lookups.

### Test-release notes

- This supersedes `0.1.0.20-test` and remains a public test build, not a stable
  release.

## [0.1.0.20-test] - 2026-07-22

### Added

- Added **Tag Destination(s)** directly below Backup Settings. New tags can
  now be saved to Jellyfin metadata, NFO files configured by Jellyfin, or both.
- NFO writes use Jellyfin's configured metadata-saver API and stop before a
  scan if any selected library is not configured to save local NFO metadata.

### Changed

- Removed “by mp3li” from the README’s main title.

### Test-release notes

- This supersedes `0.1.0.19-test` and remains a public test build, not a stable
  release.

## [0.1.0.19-test] - 2026-07-22

### Fixed

- Marked the masked API-token fields as one-time codes and excluded them from
  common third-party password managers, so Chrome does not offer to save them
  as a user login when leaving the page.

### Test-release notes

- This supersedes `0.1.0.18-test` and remains a public test build, not a stable
  release.

## [0.1.0.18-test] - 2026-07-22

### Added

- Independent **Select All** and **Select None** controls for both the provider
  and network catalogs.
- Independent provider and network search boxes that filter their own long
  checkbox lists without changing selections.

### Test-release notes

- This supersedes `0.1.0.17-test` and remains a public test build, not a stable
  release.

## [0.1.0.17-test] - 2026-07-22

### Fixed

- Removed Jellyfin's generic plugin-configuration form identity from the custom
  dashboard page. Saving is now an explicit server-wide button action, so
  navigating away cannot invoke a per-user configuration prompt.

### Test-release notes

- This supersedes `0.1.0.16-test` and remains a public test build, not a stable
  release.

## [0.1.0.16-test] - 2026-07-22

### Added

- The requested empty-state guidance in both selection lists when the required
  API credentials have not been saved.

### Test-release notes

- This supersedes `0.1.0.15-test` and remains a public test build, not a stable
  release.

## [0.1.0.15-test] - 2026-07-22

### Changed

- Replaced the combined selection wrapper with two independent, same-level
  sections: **Select Providers** and **Select Networks**.
- Provider choices now load before the first scan from TMDb's movie/TV
  provider catalogs for selected countries and Watchmode's source catalog.
- Network choices now load before the first scan from Watchmode's complete
  TV-network catalog, while retaining names discovered from media scans.

### Test-release notes

- This supersedes `0.1.0.14-test` and remains a public test build, not a stable
  release.

## [0.1.0.14-test] - 2026-07-22

### Added

- Two-column provider and network selection controls that remember all values
  discovered in selected-library scans, even after a cleanup removes current
  tags.
- No-source-lookup synchronization actions for providers and networks. Each
  creates a backup, removes only unselected plugin-owned tags of its own kind,
  and makes future scans honor the chosen list.

### Fixed

- Saved TMDb and Watchmode credentials now use the active Jellyfin theme's
  normal input background instead of a hard-coded dark autofill color.

### Test-release notes

- This supersedes `0.1.0.13-test` and remains a public test build, not a stable
  release.

## [0.1.0.13-test] - 2026-07-22

### Fixed

- Scan ETA now uses a numeric seconds value supplied by the server instead of
  attempting arithmetic on Jellyfin's serialized duration string.
- The live View Tags overview now queries each selected library directly and
  preserves that selected-library identity in its results.

### Added

- Completed scan feedback now remains visible with the checked-item total, the
  number of tags newly added, and the number of media items that received one.

### Test-release notes

- This supersedes `0.1.0.12-test` and remains a public test build, not a stable
  release.

## [0.1.0.12-test] - 2026-07-22

### Added

- Jellyfin-color retro television/question-mark branding: a transparent README
  icon and a larger catalog plugin image served through the repository manifest.

### Test-release notes

- This supersedes `0.1.0.11-test` and remains a public test build, not a stable
  release.

## [0.1.0.11-test] - 2026-07-22

### Added

- **Delete Backup** controls in Main Settings and Scan. They permanently remove
  only the selected stored backup and never alter current Jellyfin tags.
- The complete Backup Settings section to the Scan tab, so a safety snapshot
  can be made immediately before a manual scan.
- Clear country-list guidance when TMDb cannot return its watch-provider regions.

### Changed

- Shortened the dashboard page title to **Media Tagging Manager** and replaced
  its introduction with repository and Patreon links.
- Moved View Tags filters ahead of the Library Overview.
- Expanded and reorganized the public README around the current plugin settings
  tabs and controls.

### Fixed

- Creating a backup before saving a library selection now explains that no
  libraries have been saved instead of showing an unknown error.

### Test-release notes

- This supersedes `0.1.0.10-test` and remains a public test build, not a stable
  release.

## [0.1.0.10-test] - 2026-07-21

### Changed

- Rebuilt the dashboard into **Main Settings**, **View Tags**, and **Scan**
  page-style tabs, with consistent top-level section headers and descriptions.
- Reordered and redesigned backup controls, selected-library configuration, API
  settings, three-country availability selection, tag settings, incoming-media
  settings, scheduled tasks, grouped tag review, and scan controls.
- Removed administrator-configured JSON sources and the configurable parallel
  lookup setting from the product and configuration.
- Shortened the main Dashboard menu label to **Media Tagging Manager** while
  retaining the full catalog title.

### Added

- TMDb-backed availability-country dropdowns with up to three selected regions.
- Watchmode monthly quota settings and visible usage tracking. TMDb is queried
  first; Watchmode is only used as a quota-limited provider fallback.
- A Stop Scan action, selected-library list on the Scan tab, backup dropdown,
  disabled no-backup restore action, and staged manual tag edits saved from the
  View Tags tab.

### Test-release notes

- This supersedes `0.1.0.9-test` and remains a public test build, not a stable
  release.

## [0.1.0.9-test] - 2026-07-21

### Fixed

- Replaced the incorrect dashboard response-wrapper workaround with Jellyfin Web
  10.11.11's documented JSON-read pattern: `ApiClient.getJSON()` for every
  plugin GET endpoint. This parses the actual settings response before reading
  its configuration and selectable libraries.
- Shortened the Dashboard plugin-menu label to **Media Tagging Manager** while
  retaining the full catalog and page title.

### Test-release notes

- This supersedes `0.1.0.8-test` and remains a public test build, not a stable
  release.

## [0.1.0.8-test] - 2026-07-21

### Attempted fix

- Added a response-wrapper workaround for the 10.11.11 dashboard client. Live
  testing showed it did not parse `ApiClient.ajax()`'s returned `Response`.
  The precise `ApiClient.getJSON()` correction is in `0.1.0.9-test`.

### Test-release notes

- This supersedes the unuploaded `0.1.0.7-test` package and remains a public
  test build, not a stable release.

## [0.1.0.7-test] - 2026-07-21

### Fixed

- Added the supported Dashboard plugin-menu entry, using the same
  `EnableInMainMenu` page setting as File Transformation.
- Moved dashboard scan requests into Jellyfin's scheduled-task manager instead
  of using detached background tasks.
- Sent Watchmode's documented region filter and added the required JustWatch
  attribution for TMDb watch-provider data.
- Preserved managed tags when an enabled source cannot identify or query an
  item, restricted manual edits to selected Movie/Series libraries, and
  serialized backup creation/restoration with scans and manual edits.

### Documentation

- Added the source-level Jellyfin 10.11.11 compatibility audit.

### Test-release notes

- This was a local test package superseded before publication by
  `0.1.0.8-test`.

## [0.1.0.6-test] - 2026-07-21

### Fixed

- Matched Jellyfin 10.11.11's own library controller by retrieving all virtual
  folders through `ILibraryManager.GetVirtualFolders(true)`.
- Made the dashboard accept either PascalCase or camelCase API response fields
  and display a readable response error instead of silently leaving library
  selection blank.

### Test-release notes

- This supersedes the `0.1.0.5-test` catalog build and remains a public test
  build, not a stable release.

## [0.1.0.5-test] - 2026-07-21

### Fixed

- Removed the dashboard-internal configuration and virtual-folder requests that
  could leave the settings page in an endless global loading state.
- Added administrator-only plugin endpoints that retrieve selectable Jellyfin
  libraries and save plugin settings directly through Jellyfin server services.

### Test-release notes

- This supersedes the `0.1.0.4-test` catalog build and remains a public test
  build, not a stable release.

## [0.1.0.4-test] - 2026-07-21

### Changed

- Rebuilt the plugin against the official Jellyfin.Controller and Jellyfin.Model
  `10.11.11` packages for the active Jellyfin 10.11.11 test server.
- Updated the test catalog ABI declaration to `10.11.11.0`.

### Test-release notes

- This supersedes the `0.1.0.3-test` catalog build and remains a public test
  build, not a stable release.

## [0.1.0.3-test] - 2026-07-21

### Fixed

- Load plugin settings and Jellyfin libraries immediately when the dashboard
  configuration page opens, avoiding dashboard variants that do not emit the
  expected page-show event.

### Changed

- Replaced the Scan, Settings & sources, and Library overview tabs with one
  continuous, scrollable administrator page.

### Test-release notes

- This supersedes the `0.1.0.2-test` catalog build and remains a public test
  build, not a stable release.

## [0.1.0.2-test] - 2026-07-21

### Fixed

- Corrected the Jellyfin dashboard lifecycle event so the plugin now loads the
  selectable library list, saved configuration, scan controls, and backup list.
- Added visible errors for failed settings loads, saves, manual backup creation,
  and Undo so an unsuccessful request no longer appears to do nothing.
- Clarified that a library must be selected and saved before a manual backup can
  be created.

### Test-release notes

- This supersedes the `0.1.0.1-test` catalog build and remains a public test
  build, not a stable release.

## [0.1.0.1-test] - 2026-07-21

### Added

- Jellyfin plugin foundation targeting Jellyfin 10.11.3.
- Administrator dashboard for library selection, tag behavior, region, source configuration, scans, filtering, and manual corrections.
- Explicit overlap-safe Jellyfin tags: `Provider: <name>` and `Network: <name>`.
- TMDb availability/network adapter, Watchmode availability adapter, and configurable custom JSON source adapter.
- Per-library and all-selected-library manual scans.
- Scan status with active title, completed count, progress percentage, and estimated remaining time.
- Native scheduled full-refresh task with an administrator-configurable interval.
- Optional incoming-media checks after Jellyfin library scans; disabled by default and independent from full refreshes.
- Complete selected-library tag backups before tag-changing scans, incoming-media updates, and manual tag edits.
- Named backup creation, backup list, per-backup restore, and Undo last tag operation in plugin settings.
- Safeguards that preserve existing plugin-managed tags when no source is configured or all enabled sources fail.
- Documentation/API_KEYS.md with per-server credential setup, safe rotation guidance, and a non-secret custom JSON example.
- Documentation/project-goals.txt for product-scope and delivery tracking.
- GPL-3.0-or-later licensing and package metadata for the public source repository.
- A compact README TMDb credential walkthrough, truthful application-form wording, and visible TMDb attribution in the plugin settings.

### Changed

- Renamed the project and displayed plugin name to Media Tagging Manager Jellyfin Plugin.
- Renamed the plugin assembly and source namespace to `Jellyfin.Plugin.MediaTaggingManager`.
- Moved built-in source credentials from request URLs to HTTP headers.
- Improved dashboard provider and network filters to support partial-text matching.

### Test-release notes

- This is a public, catalog-install test build—not a stable release.
- The test manifest points to a real ZIP for Jellyfin 10.11.3 and includes that ZIP's real checksum.
- The ZIP, manifest, and repository contain no administrator API keys, Jellyfin configuration, backups, logs, or media data.
- Runtime testing in an installed Jellyfin server remains required before the first stable release.
