# Changelog

All notable changes to Media Tagging Manager Jellyfin Plugin by mp3li are documented in this file.

The format follows the spirit of [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), with release entries added only when a real packaged release exists.

## [Unreleased]

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
