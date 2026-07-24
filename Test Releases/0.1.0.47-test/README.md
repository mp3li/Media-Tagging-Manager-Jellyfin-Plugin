# 0.1.0.47-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, or saved logos.

It supersedes `0.1.0.46-test` by saving the Provider, Network, Genre, and
availability-region controls directly from their current visible values. It no
longer depends on separately tracked browser draft state or reloads source
catalogs after the Network and Provider Settings save.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
