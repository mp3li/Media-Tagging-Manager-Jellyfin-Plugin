# 0.1.0.57-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, saved provider/network logos, stored TMDb
relationships, or cached posters.

It adds **More Like This Settings**, the second tab in the secondary settings
row. Normal scans can save TMDb's direct Recommendation and Similar Title
lists for selected-library Movies and Series, separate from Jellyfin tags and
NFO metadata. The tab includes optional lightweight poster links, optional
bounded local poster caching, and a colored additions/removals overview.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
