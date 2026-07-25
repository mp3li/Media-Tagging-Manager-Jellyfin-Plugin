# 0.1.0.62-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, stored TMDb relationships, or cached images.

It makes the Recommendations and Similar Titles overview responsive for large
libraries. The tab now requests small per-library pages only after you expand a
library, and displays ten source media items at a time instead of creating all
recommendation and similar-title cards in one page render.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
