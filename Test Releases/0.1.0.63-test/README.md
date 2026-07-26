# 0.1.0.63-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, saved TMDb data, or cached images.

It adds the final two secondary settings tabs: **Ratings Settings** and
**Spoken Languages and Translations Settings**. Both have scoped saving,
dedicated Jellyfin-managed selected-library loading, progress, native Jellyfin
metadata writes where Jellyfin supports the field, and collapsed color-coded
per-library overviews. Ratings supports community rating, vote count,
country-specific classifications, an optional primary native classification,
and adult flags. Language settings supports original language, spoken
languages, and all available TMDb translations.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
