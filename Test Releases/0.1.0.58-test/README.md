# 0.1.0.58-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, stored TMDb relationships, or cached posters.

It adds dedicated **Load**, **Sync**, and **Remove Recommendations and Similar
Titles** controls. Load/sync run through Jellyfin's task manager for saved
selected libraries without running the complete tag scan. Remove deletes only
plugin-stored relationship records and leaves Jellyfin metadata untouched.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
