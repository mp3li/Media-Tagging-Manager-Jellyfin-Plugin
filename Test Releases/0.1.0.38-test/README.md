# 0.1.0.38-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, or saved logos.

It supersedes `0.1.0.37-test` by removing the settings-recovery code entirely.
It uses Jellyfin's normal plugin configuration persistence and does not restore,
score, merge, or select any older settings copy.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
