# 0.1.0.48-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, or saved logos.

It supersedes `0.1.0.47-test` by correctly parsing JSON returned by Jellyfin's
settings and result endpoints. Previous builds treated those responses as raw
browser response objects, which could redraw saved selections as empty/default
values.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
