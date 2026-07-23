# 0.1.0.37-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, saved logos, or settings-recovery copies.

It supersedes `0.1.0.36-test` with an urgent fix for update-time settings loss.
It detects a valid blank default configuration, restores the best saved
current/previous server-local recovery copy, and reports the recovery decision
without exposing settings or credentials.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
