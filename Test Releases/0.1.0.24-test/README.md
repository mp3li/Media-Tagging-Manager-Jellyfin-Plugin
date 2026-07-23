# 0.1.0.24-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and no API
keys, Jellyfin configuration, backups, logs, NFO files, or media data.

It returns specific safe explanations when a provider/network selection sync
cannot run. It also adds the optional **Group different types of the same
provider** preference. It is off by default; when enabled, it groups only the
documented Netflix, Apple TV, and Amazon/Prime provider variants.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
