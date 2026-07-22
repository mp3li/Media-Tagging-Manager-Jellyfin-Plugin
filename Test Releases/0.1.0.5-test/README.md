# 0.1.0.5-test catalog-install build

This folder contains the test ZIP that replaces hanging dashboard-internal
settings and library calls with administrator-only endpoints in this plugin.

- Target Jellyfin ABI: `10.11.11.0`
- Plugin version: `0.1.0.5`
- Purpose: retest initial settings load, library selection, settings save,
  backups, scans, and update behavior on a Jellyfin 10.11.11 server.

This is not a stable release. It contains no API credentials, Jellyfin
configuration, backups, logs, or media data.
