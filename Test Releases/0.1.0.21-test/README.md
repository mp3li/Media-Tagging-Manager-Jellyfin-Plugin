# 0.1.0.21-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and no API
keys, Jellyfin configuration, backups, logs, NFO files, or media data.

It tracks Watchmode requests in the active 30-day cycle calculated from the
administrator-entered **Quota Resets On** date. It also counts the plugin's
Watchmode reference-catalog calls against that same limit.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
