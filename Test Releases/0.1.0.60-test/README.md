# 0.1.0.60-test

This public Jellyfin `10.11.11` catalog-install test build is not a stable
release. It contains only `Jellyfin.Plugin.MediaTaggingManager.dll` and the
repository `LICENSE`; it contains no API keys, Jellyfin configuration, backups,
logs, NFO files, media data, stored TMDb relationships, or cached images.

It adds **Production Companies and Countries Settings**. The tab can fill
missing native Jellyfin Studio and production-country metadata from direct
TMDb title details, retain existing metadata, filter production countries with
a searchable allow-list, cache one company logo per company, and clean up only
values recorded as plugin-added.

It also makes the dedicated **Load Recommendations and Similar Titles** action
explain its exact outcome beneath its progress bar: saved relationship counts,
items without TMDb IDs, TMDb lookup failures, and valid empty relationship
responses. This lets an empty relationship overview be diagnosed directly from
the plugin rather than appearing unexplained.

Record live results in [goal-testing.txt](../../Documentation/goal-testing.txt).
