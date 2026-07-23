# 0.1.0.22-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and no API
keys, Jellyfin configuration, backups, logs, NFO files, or media data.

It sends all TMDb requests through a shared 35-requests-per-second gate. An
HTTP 429 pauses TMDb requests and retries the affected safe read up to two
times, honoring `Retry-After` when TMDb provides it.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
