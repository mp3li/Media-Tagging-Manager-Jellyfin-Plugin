# 0.1.0.53-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, or saved logos.

It makes Network scan accounting exact: the completed-scan summary now shows
source returns, final selected Network candidates, already-present tags,
explicit Streaming app only exclusions, and Network tags actually added. The
Network-catalog status also identifies its TMDb and Watchmode source counts.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
