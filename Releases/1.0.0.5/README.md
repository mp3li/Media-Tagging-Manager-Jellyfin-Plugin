# 1.0.0.5

This stable catalog update expands genre visibility and fixes manually uploaded
logos for unknown Provider and Network tags.

- Shows every native Jellyfin genre imported from NFO `<genre>` fields in the
  Genres and Keywords Library Overview, even when the name is outside TMDb's
  selectable genre catalog.
- Combines native genres and prefixed `Genre:` tags for display and filtering,
  with case-insensitive duplicate suppression.
- Keeps native Jellyfin genres read-only; overview edits continue to change only
  prefixed Genre and Keyword tags.
- Sends selected PNG, JPEG, and SVG logos as real multipart uploads for mapped
  unknown Provider and Network tags.
- Reports the server's actual rejection reason and distinguishes a saved official
  name from a failed logo upload.
- Refreshes the dashboard logo cache after a successful upload.

The catalog ZIP contains only the plugin DLL and the project license. Release
build, package, manifest, and static dashboard checks do not replace a live
Jellyfin server/browser test.
