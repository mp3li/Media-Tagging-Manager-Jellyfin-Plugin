# 1.0.0.4

This stable catalog update protects tags imported from NFO files or created by
other tools.

- Adds an exact per-item ownership record only when Media Tagging Manager adds
  a previously absent tag.
- Prevents API omissions from removing unowned Provider, Network, Genre, or
  Keyword tags.
- Starts upgrades with no ownership claims over existing library tags.
- Clears stale ownership claims before restoring a complete tag backup.
- Applies the same ownership protection to full, scheduled, incoming-media,
  manual, selection-sync, and cleanup operations.

The catalog ZIP contains only the plugin DLL and the project license.
