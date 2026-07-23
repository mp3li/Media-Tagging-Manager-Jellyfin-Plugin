# 0.1.0.31-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and no API
keys, Jellyfin configuration, backups, logs, NFO files, media data, or saved
logos.

It returns Provider and Network picker names before optional bulk catalog-logo
caching, preventing a large logo-cache request from making the pickers appear
empty after a settings save.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
