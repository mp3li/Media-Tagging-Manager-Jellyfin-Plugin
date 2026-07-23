# 0.1.0.30-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and no API
keys, Jellyfin configuration, backups, logs, NFO files, media data, or saved
logos.

It fixes empty Provider/Network selections so they affect only future tag
additions, and makes Library Overview read the saved selected-library setting
instead of relying on hidden dashboard checkbox controls.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
