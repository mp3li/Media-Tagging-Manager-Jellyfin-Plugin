# 0.1.0.9-test

This is a public Jellyfin `10.11.11` catalog-install test build, not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and no API
keys, Jellyfin configuration, backups, logs, or media data.

This build follows Jellyfin Web 10.11.11's exact JSON-read behavior:
`ApiClient.getJSON()` is used for every plugin GET endpoint, so the settings
response and selectable libraries are parsed before the page reads them. The
Dashboard menu label is now **Media Tagging Manager**.

Record all real-server results in
[goal-testing.txt](../../Documentation/goal-testing.txt).
