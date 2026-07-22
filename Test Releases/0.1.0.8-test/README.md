# 0.1.0.8-test

This is a public Jellyfin `10.11.11` catalog-install test build, not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and no API
keys, Jellyfin configuration, backups, logs, or media data.

This build normalizes Jellyfin 10.11.11 dashboard transport wrappers before
reading plugin settings and libraries. It also includes the supported Dashboard
plugin-menu entry and Jellyfin task-managed scanning introduced in the
unpublished 0.1.0.7-test package.

Record all real-server results in
[goal-testing.txt](../../Documentation/goal-testing.txt).
