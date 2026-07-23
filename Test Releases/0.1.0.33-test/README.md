# 0.1.0.33-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and no API
keys, Jellyfin configuration, backups, logs, NFO files, media data, or saved
logos.

**Load All Logos** now preloads Provider and Network picker logos before any
media scan. Network images use a documented TMDb network ID from the Network
catalog and remain blank only when the source has no TMDb mapping or TMDb has
no logo for that network. It does not scan media or modify tags.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
