# 0.1.0.35-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, saved logos, or settings-recovery copies.

It supersedes `0.1.0.34-test` with an update-safe, server-local settings
recovery mirror. The plugin validates its primary Jellyfin configuration before
Jellyfin can replace unreadable settings with defaults, and restores the most
recent known-good mirror when needed.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
