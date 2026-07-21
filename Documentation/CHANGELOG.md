# Changelog

All notable changes to Media Tagging Manager Jellyfin Plugin by mp3li are documented in this file.

The format follows the spirit of [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), with release entries added only when a real packaged release exists.

## [Unreleased]

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

### Changed

- Renamed the project and displayed plugin name to Media Tagging Manager Jellyfin Plugin.
- Renamed the plugin assembly and source namespace to `Jellyfin.Plugin.MediaTaggingManager`.
- Moved built-in source credentials from request URLs to HTTP headers.
- Improved dashboard provider and network filters to support partial-text matching.

### Notes

- This is not a packaged public release yet.
- A public repository manifest will be added only after a real release ZIP, public download URL, target ABI, and checksum exist.
- Runtime testing in an installed Jellyfin server remains required before the first public release.
