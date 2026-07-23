# 0.1.0.32-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and no API
keys, Jellyfin configuration, backups, logs, NFO files, media data, or saved
logos.

It adds explicit background **Load All Logos** and **Load Logos for Selected
Providers** actions, a configurable 10 MB–1 GB logo-cache limit (100 MB by
default), a 2 MB source-logo limit, visible cache/loading status, and selective
cached-logo deletion. These controls do not scan media or modify tags.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
