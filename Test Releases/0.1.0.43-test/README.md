# 0.1.0.43-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, or saved logos.

It supersedes `0.1.0.42-test` with native Jellyfin metadata saving. Jellyfin's
own library settings now control NFO output, so the plugin no longer makes a
second direct NFO write. It also includes the scoped settings-persistence
reliability corrections documented in the compatibility audit.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
