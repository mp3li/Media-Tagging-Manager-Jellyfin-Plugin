# 0.1.0.54-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, saved logos, or person images.

It adds the Cast and Crew Settings tab. Opted-in full scans can append only
missing TMDb cast and selected-job crew credits while preserving current
Jellyfin people metadata, then fill missing shared Jellyfin person photos. It
also adds a dedicated photo-only task and conservative cleanup actions that
affect only records this plugin created.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
