# 0.1.0.46-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, or saved logos.

It supersedes `0.1.0.45-test` by treating Provider, Network, and Genre picker
changes as explicit actions. A tab-level Save now preserves a previously saved
list unless that exact picker was changed; **Select None** still deliberately
saves an empty list.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
