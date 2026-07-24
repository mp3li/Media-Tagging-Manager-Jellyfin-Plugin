# 0.1.0.44-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, or saved logos.

It supersedes `0.1.0.43-test` by making Watchmode quota corrections safe:
saving the quota reset date, limit, or current usage does not reload source
catalogs or spend Watchmode requests. Catalogs refresh only when a TMDb token
or Watchmode API key changes.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
