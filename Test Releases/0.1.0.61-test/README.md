# 0.1.0.61-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, stored TMDb relationships, or cached images.

It fixes the dedicated **Load Recommendations and Similar Titles** action
storing relationship records under a hierarchy parent instead of the exact
selected library. That made a later overview show zero records even though
relationship data had been retrieved. Load now automatically revisits and
repairs those old records, then displays them under the selected library.

It also adds a dedicated **Load Production Companies and Countries** action.
After you save Production Settings, it fetches enabled production metadata for
selected libraries without running the full tag scan, reports progress and
completion, refreshes its colored overview, and caches source-supplied company
logos when that setting is enabled.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
