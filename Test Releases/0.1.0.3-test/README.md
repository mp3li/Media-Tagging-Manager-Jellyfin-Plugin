# 0.1.0.3-test catalog-install build

This folder contains the test ZIP that loads configuration immediately for
Jellyfin dashboard variants that do not emit a plugin page-show event. It also
uses one continuous settings page rather than separate tabs.

- Target Jellyfin ABI: `10.11.3.0`
- Plugin version: `0.1.0.3`
- Purpose: retest catalog installation, library selection, settings, backups,
  scans, and update behavior on a real Jellyfin server.

This is not a stable release. It contains no API credentials, Jellyfin
configuration, backups, logs, or media data.
