# 0.1.0.64-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, saved TMDb data, or cached images.

It fixes the Ratings Settings dashboard persistence regression: saving that tab
now preserves every selected classification, Primary Jellyfin Classification
Country is populated before any checkbox selection, and all tab labels remain
on one line. It also adds selected-classification sync and separate Spoken
Languages and Translations cleanup actions.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
