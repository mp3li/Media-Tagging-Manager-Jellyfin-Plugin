# 0.1.0.20-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and no API
keys, Jellyfin configuration, backups, logs, NFO files, or media data.

It adds **Tag Destination(s)**: save new tags in Jellyfin, to NFO files through
Jellyfin's configured NFO metadata saver, or to both destinations. A scan stops
before changes if an NFO-selected library is not configured to save local NFO
metadata.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
