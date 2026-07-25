# Changelog

All notable changes to Media Tagging Manager Jellyfin Plugin by mp3li are documented in this file.

The format follows the spirit of [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), with release entries added only when a real packaged release exists.

## [0.1.0.62-test] - 2026-07-25

### Fixed

- Reworked the **Recommendations and Similar Titles Additions and Removals**
  overview for large libraries. It now loads lightweight selected-library
  counts first, fetches details only when an individual library is expanded,
  and displays ten source media items per page instead of rendering every
  relationship card at once.

## [0.1.0.61-test] - 2026-07-25

### Fixed

- Fixed the dedicated **Load Recommendations and Similar Titles** action using
  an item hierarchy parent rather than the exact selected library identifier.
  That could save valid relationship records outside the overview's selected
  library scope. Load now repairs those older misplaced records automatically
  as well as loading genuinely missing records.

### Added

- Added a dedicated Jellyfin-managed **Load Production Companies and Countries**
  action with progress and completion feedback. It fetches only this tab's
  enabled metadata for selected libraries, updates the production overview and
  colors, and caches source-supplied company logos when enabled—without running
  the complete tag scan.

## [0.1.0.60-test] - 2026-07-25

### Added

- Added **Production Companies and Countries Settings** as a third secondary
  tab. It fills missing native Jellyfin Studio and production-country metadata
  from direct TMDb title details, with a searchable country allow-list,
  conservative cleanup provenance, production-company logo caching, direct
  editing, and a collapsible additions/removals overview.
- More Like This completion feedback now reports missing TMDb IDs, TMDb lookup
  failures, and valid empty relationship responses alongside saved-result
  counts.

## [0.1.0.59-test] - 2026-07-25

### Fixed

- Replaced the generic More Like This action route with distinct, explicit
  Jellyfin dashboard routes for **Load** and **Update**, resolving the failed
  Load action while keeping the work queued through Jellyfin's task manager.
- Renamed **Sync Recommendations and Similar Titles** to **Update
  Recommendations and Similar Titles** throughout the dashboard and
  documentation. Update retains the former Sync behavior: it refreshes saved
  selected-library records and removes relationships TMDb no longer returns.

## [0.1.0.58-test] - 2026-07-25

### Added

- Added a dedicated Jellyfin-managed **Load Recommendations and Similar
  Titles** action, so selected-library TMDb relationship data can be populated
  without running the full tag scan.
- Added **Sync Recommendations and Similar Titles**, record-only removal,
  progress, and completion feedback. Sync refreshes existing selected-library
  records and retains exact removed titles for the colored overview.

## [0.1.0.57-test] - 2026-07-25

### Added

- Added the second secondary-row **More Like This Settings** tab. Normal
  selected-library scans can now store TMDb’s direct Recommendations and
  Similar Titles separately from Jellyfin tags and NFO metadata.
- Added optional lightweight TMDb poster-link storage, optional bounded local
  poster caching (5 MB per poster and configurable total limit), and a
  collapsed, library-grouped Recommendations and Similar Titles overview with
  portrait posters and accessible addition/removal colors.

## [0.1.0.56-test] - 2026-07-25

### Fixed

- The Cast and Crew overview now displays each available Jellyfin person image
  as a small thumbnail beside the related cast/crew or photo-list name.

## [0.1.0.55-test] - 2026-07-24

### Added

- Added the always-available, collapsed **Cast and Crew Additions and
  Removals** overview. It shows current selected-library people metadata before
  any scan, then overlays exact newest-operation additions/removals in
  administrator-selected colors.

### Fixed

- The Cast and Crew secondary tab no longer also highlights Main Settings.
- Added the required save-before-scanning reminder and removed duplicated
  completed photo-scan totals from the dashboard status line.

## [0.1.0.54-test] - 2026-07-24

### Added

- Added a secondary **Cast and Crew Settings** dashboard tab with separately
  saved cast, crew-job, and missing-photo choices.
- Full selected-library scans can now append missing TMDb cast and selected-job
  crew credits while preserving current Jellyfin credits, character names, and
  cast ordering.
- Added a Jellyfin-managed **Scan for Cast and Crew Photos** task with live
  progress, missing-photo, TMDb-availability, saved-photo, and storage totals.
- Added conservative cleanup actions backed by private plugin provenance. They
  remove only exact credits or person image paths this plugin recorded as
  creating; no visible Jellyfin tag is used for that bookkeeping.

## [0.1.0.53-test] - 2026-07-24

### Fixed

- Corrected Network scan accounting so Watchmode Network names returned during
  a Provider-only fallback are no longer presented as Network candidates.
- Added final-stage Network counts for selected candidates, already-present
  tags, values intentionally withheld by Streaming app only, and Network tags
  actually added. This makes a Network source result distinguishable from a
  final Jellyfin tag write.
- Network-catalog status now records and displays its separate TMDb and
  Watchmode source counts alongside its unique cached Network total.

## [0.1.0.52-test] - 2026-07-24

### Added

- Added an explicit **Load Networks** action above the Network picker. It
  builds a local catalog for the saved availability regions without scanning
  media or changing tags, and reports its progress while TMDb Network origins
  are checked.
- The Network catalog merges TMDb's country-verified Networks with
  Watchmode's own country-filtered Network catalog when that optional source
  is configured. The cached TMDb IDs also let **Load All Logos** preload
  eligible Network logos for the Network picker.

### Fixed

- Corrected Network fallback selection: an unselected TMDb Provider or Network
  can no longer suppress a selected, source-returned Watchmode fallback value.
- Added scan-completion Network diagnostics showing TMDb and Watchmode Network
  returns, values excluded by the saved Network allow-list, and lookup
  failures.
- Opening dashboard settings no longer automatically requests Watchmode's
  complete Network catalog. Network data is loaded only by the explicit
  Network action or reused from its matching local cache.

## [0.1.0.51-test] - 2026-07-24

### Fixed

- Rebalanced the Provider, Network, Genre, Keyword, and Collection review
  tables: Title no longer consumes most of the width, Provider and Network are
  visibly separated, and tag names wrap within their own readable columns.

## [0.1.0.50-test] - 2026-07-24

### Added

- Added per-library expand/collapse controls to both Library Overview and
  Additions and Removals in the Last Scan.
- Added **Remove Genres Added by This Plugin**. It makes a safety backup,
  removes only plugin-created `Genre:` tags, and turns off future genre tagging
  until genres are selected and saved again.
- Provider, Network, and Genre selection sync actions now replace the latest
  scan-change review with their local no-source tag removals; the next full
  scan replaces that history again.

### Fixed

- Made the last-scan color controls compact square color pickers and made the
  collapsed state explicit so it cannot be overridden by dashboard styling.
- Renamed **Save Last Scan Colors** to **Save Colors**.
- Replaced the one-field-at-a-time browser prompt editor with one accessible
  tag-edit window containing separate Provider, Network, Genre, Keyword, and
  Collection fields. The same window is used from both tag-overview views.

## [0.1.0.49-test] - 2026-07-24

### Added

- Added the collapsed-by-default **Additions and Removals in the Last Scan**
  section to Scan. It groups only the media changed by the newest scan using
  the same Provider, Network, Genre, Keyword, and Collection layout and
  editing controls as Library Overview.
- Added administrator-configurable added-tag and removed-tag colors, defaulting
  to green and red respectively, for accessibility.
- Kept last-scan change classifications separate from the normal overview.
  Editing a last-scan row carries its own green/red classifications forward and
  cannot recolor unrelated tags or items.

## [0.1.0.48-test] - 2026-07-24

### Fixed

- Corrected dashboard POST/PUT response handling for Jellyfin Web 10.11.11.
  The browser now explicitly parses JSON returned by settings and result
  endpoints instead of treating a raw `Response` object as an empty plugin
  configuration.
- Kept raw responses only for endpoints that intentionally return `202` or
  `204`, including scan requests, manual tag edits, and delete actions.

## [0.1.0.47-test] - 2026-07-24

### Fixed

- Replaced the event-derived Provider, Network, Genre, and availability-region
  draft state with Jellyfin's native control-read pattern: each save reads the
  visible checkbox and dropdown values at the moment the administrator presses
  Save.
- Stopped the Network and Provider tab from reloading source catalogs after its
  own save, eliminating an immediate post-save redraw that could disagree with
  the saved configuration.

## [0.1.0.46-test] - 2026-07-24

### Fixed

- Made Provider, Network, and Genre selections explicit, independently saved
  settings. A tab-level save now preserves each saved list unless that exact
  picker was changed, while **Select None** remains an intentional saved empty
  selection.
- Captured checkbox interactions at each picker container so Jellyfin's custom
  dashboard controls cannot lose an in-progress selection before its dedicated
  or tab-level save completes.

## [0.1.0.45-test] - 2026-07-24

### Fixed

- Preserved in-progress and saved Provider, Network, Genre, and three-country
  availability selections when a source catalog or country list redraws.
  Bottom-of-tab saves now use stable selection state rather than a transient
  picker DOM snapshot.

## [0.1.0.44-test] - 2026-07-24

### Fixed

- Correcting Watchmode quota details or **Current API Usage** no longer reloads
  source catalogs or makes Watchmode requests. Catalogs refresh only after a
  changed TMDb token or Watchmode API key. An unrelated Main Settings save also
  preserves API and quota values unless those controls were edited.

## [0.1.0.43-test] - 2026-07-24

### Fixed

- Removed **Tag Destination(s)**. Every tag update now uses Jellyfin's one
  native metadata-update workflow. If a library is configured in Jellyfin to
  save NFO metadata, Jellyfin controls that output itself; the plugin no longer
  makes a second direct NFO-save call that could write the same NFO twice.
- Corrected cached-logo image URLs to include Jellyfin's administrator access
  token. The logo endpoint was already protected, so the prior unauthenticated
  browser image requests failed and their broken-image handler removed them.
- Added scoped configuration updates so a save from one dashboard tab does not
  replace unrelated saved settings with stale values.

## [0.1.0.42-test] - 2026-07-23

### Fixed

- Replaced the shared full-configuration save with separate server-side save
  paths for Main Settings, Network and Provider Settings, Genres and Keywords,
  and Scheduled Tasks. Each path updates only its own settings.
- Made Scan, backups, collection matching, and the Scan-tab library summary use
  the saved library selection rather than hidden dashboard checkbox state.
- Added immediate queued-scan feedback so a dashboard scan request cannot look
  like a no-op while Jellyfin's task manager starts it.

### Test-release notes

- This supersedes `0.1.0.41-test` and remains a public test build, not a
  stable release.

## [0.1.0.41-test] - 2026-07-23

### Fixed

- Stopped Provider/Network picker catalogs from being written into Jellyfin's
  saved plugin configuration. Only administrator-selected Provider and Network
  names are now saved; full source catalogs remain temporary picker data.
- Protected the settings endpoint from accepting dashboard-supplied catalog
  arrays, preserving only names discovered by actual title scans.

### Test-release notes

- This supersedes `0.1.0.40-test` and remains a public test build, not a
  stable release.

## [0.1.0.40-test] - 2026-07-23

### Fixed

- Stopped the Watchmode quota-reset date from blocking unrelated settings saves.
  Watchmode remains safely unavailable until that date is set, but API,
  Provider/Network, Genre, and other settings can now save independently.
- Improved dashboard error text when a settings request fails.
- Clarified scan completion totals: all selected-library items are checked,
  while only items receiving new tags are counted as changed.
- Corrected picker-logo refresh URLs so Jellyfin authentication parameters are
  retained when a cached logo is refreshed after loading.

### Test-release notes

- This supersedes `0.1.0.39-test` and remains a public test build, not a
  stable release.

## [0.1.0.39-test] - 2026-07-23

### Fixed

- Added **Save API Settings** directly below the credentials. It and **Save
  Main Settings** use the same save path.
- Prevented Main Settings saves from reading or changing Provider/Network
  selection controls. Those controls are now changed only by their own settings
  tab and its save controls.
- Protected already-saved API credentials from blank values posted by a
  settings section that does not edit credentials.
- Reset the logo progress bar to its empty dark state when loading finishes,
  refreshed cached logo URLs after completion, and preserved Jellyfin's
  authenticated image URL parameters while doing so.
- Displayed picker logos after the Provider or Network name in a smaller size.

### Test-release notes

- This supersedes `0.1.0.38-test` and remains a public test build, not a
  stable release.

## [0.1.0.38-test] - 2026-07-23

### Fixed

- Removed the settings-recovery layer introduced in the recent test releases.
  It could choose an older server-local copy during startup, which is not an
  acceptable substitute for preserving the current Jellyfin plugin
  configuration. Configuration now uses Jellyfin's standard `BasePlugin`
  persistence path only.

### Test-release notes

- This supersedes `0.1.0.37-test`. It intentionally does not attempt to
  restore, score, merge, or select a prior settings copy.

## [0.1.0.37-test] - 2026-07-23

### Fixed

- Corrected the settings-recovery decision for a valid but empty default
  configuration created during an update. The plugin now restores the most
  meaningful available current/previous server-local recovery copy rather than
  accepting a blank default and replacing saved credentials and preferences.
- Added an explicit dashboard-save timestamp, so a deliberately cleared
  configuration is not mistaken for an update reset.
- Added a non-sensitive **Settings recovery status** message in API Settings
  that reports whether the current or previous recovery copy was used.

### Test-release notes

- This supersedes `0.1.0.36-test` and is an urgent settings-persistence
  regression fix. It remains a public test build, not a stable release.

## [0.1.0.36-test] - 2026-07-23

### Fixed

- Added an editable **Current API Usage** field beside Watchmode's quota-reset
  date. An administrator can seed or correct the current 30-day-cycle count;
  later plugin requests add to that value, while authoritative Watchmode quota
  headers still correct it when supplied.
- Filtered Watchmode's Network picker catalog to the selected availability
  countries using its `origin_country` metadata instead of displaying the full
  worldwide catalog.
- Added a visible logo-loading progress bar and count. When loading completes,
  the dashboard refreshes cached provider/network logos into its pickers and
  Library Overview.
- Background configuration mutations, including Watchmode usage updates, now
  refresh the server-local settings recovery mirror.

### Test-release notes

- This supersedes `0.1.0.35-test` and remains a public test build, not a
  stable release.

## [0.1.0.35-test] - 2026-07-23

### Fixed

- Added an update-safe, server-local settings recovery mirror. Before Jellyfin
  can replace a missing or unreadable plugin configuration XML with defaults,
  the plugin restores the last successfully saved configuration mirror.
- The recovery mirror retains the current and immediately preceding saved
  settings, including library choices, API credentials, tag selections, and
  other administrator settings. It is stored only in the server's data area,
  never in a package, manifest, backup, or repository file.

### Test-release notes

- This supersedes `0.1.0.34-test` and must be tested by updating an existing
  configured installation before it can be considered validated.

## [0.1.0.34-test] - 2026-07-23

### Fixed

- Included the Media Tagging Manager Noncommercial License 1.0 with the test
  ZIP so the installed DLL is accompanied by its license terms and required
  credit.

### Test-release notes

- This supersedes `0.1.0.33-test` and remains a public test build, not a
  stable release.

## [0.1.0.33-test] - 2026-07-23

### Changed

- **Load All Logos** now preloads Network picker logos before a media scan by
  using the documented TMDb network IDs returned by the Network catalog. A
  network remains blank only when its source catalog has no TMDb mapping or
  TMDb has no logo for that network.
- Replaced the incomplete GPL notice with the Media Tagging Manager
  Noncommercial License 1.0. It permits noncommercial forks and redistribution
  with retained credit, while requiring written permission for commercial use.

### Test-release notes

- This supersedes `0.1.0.32-test` and remains a public test build, not a
  stable release.

## [0.1.0.32-test] - 2026-07-23

### Added

- Added explicit **Load All Logos** and **Load Logos for Selected Providers**
  background actions. They do not scan media or modify tags.
- Added visible cache/loading status, a configurable 10 MB–1 GB total cache
  limit (100 MB by default), a 2 MB per-source-logo limit, and selective
  cached-logo deletion.

### Changed

- Source catalog Provider logos now load only when an administrator explicitly
  chooses a logo-loading action, so Provider/Network picker responses remain
  independent of bulk image downloads.

### Test-release notes

- This supersedes `0.1.0.31-test` and remains a public test build, not a
  stable release.

## [0.1.0.31-test] - 2026-07-23

### Fixed

- Provider and Network catalog names now return before optional catalog-logo
  caching completes, preventing a large logo cache from blanking the pickers
  after settings are saved.

### Test-release notes

- This supersedes `0.1.0.30-test` and remains a public test build, not a
  stable release.

## [0.1.0.30-test] - 2026-07-23

### Fixed

- Saving an empty Provider or Network selection now prevents future additions
  without allowing a later scan to remove existing tags. Only the explicit
  corresponding Sync action can remove unselected Provider or Network tags.
- Library Overview now reads the saved selected-library configuration directly,
  so it remains populated when its Main Settings checkbox controls are hidden.

### Test-release notes

- This supersedes `0.1.0.29-test` and remains a public test build, not a
  stable release.

## [0.1.0.29-test] - 2026-07-23

### Fixed

- Corrected the Unknown Providers and Networks **See Items** dialog's hidden
  state so it does not block the View and Edit Tags tab on page load and its
  Close button works normally.

### Test-release notes

- This supersedes `0.1.0.28-test` and remains a public test build, not a
  stable release.

## [0.1.0.28-test] - 2026-07-23

### Added

- Added **Genres and Keywords Settings** with a searchable, scrollable TMDb
  genre picker, independent Select All/Select None controls, a saved genre
  allow-list, and **Sync with Only Selected Genres**.
- Added opt-in TMDb keyword tagging. Keywords are written as `Keyword:` tags
  only when enabled before a scan, and can be removed with the backup-protected
  **Remove Keywords Added by This Plugin** action without an API request.
- Added **Collections Tags Settings**. It scans selected libraries for direct
  TMDb movie collection matches, groups reviewable results by library, and adds
  only administrator-selected `Collection:` tags after a backup.
- Expanded **View and Edit Tags** so Provider, Network, Genre, Keyword, and
  Collection tags are separate columns and separately editable.
- Added **See Items** for Unknown Providers and Networks, opening the exact
  selected-library items that carry the chosen unknown tag.

### Changed

- Scheduled outdated-availability replacement remains limited to Provider and
  Network tags. Genre, Keyword, and Collection tags use their dedicated
  controls and are not removed by that availability setting.
- Logo preferences now save with **Save Main Settings**; disabling logo use
  also stops dashboard logo requests. The redundant standalone save button was
  removed.

## [0.1.0.27-test] - 2026-07-23

### Added

- Provider and Network picker rows and View Tags now use a server-side cache
  of source-supplied logos. The cache keeps one image per normalized tag name,
  not one image per media item, and exposes those cached images to compatible
  local plugins.
- Added **Logo Settings** to stop logo use/saving and explicitly delete all
  cached or manually uploaded logos without changing media tags.
- Added **Unknown Providers and Networks**, limited to unknown prefixed
  `Provider:`/`Network:` tags. It supports persistent official-name mappings
  and a single manually uploaded PNG, JPEG, or SVG logo per mapping.
- Added a reserved, empty **Genre Settings** dashboard tab.
- Added a one-click **Clear Filters** action and a collapsed-by-default
  Library Overview in **View and Edit Tags**.

### Changed

- Clarified that TMDb and Watchmode can each return an actual title-level
  Network tag, and that the TV Network Streaming Apps **Both** mode requires
  selecting the app under Providers and its network under Networks.
- Reorganized dashboard settings into **Main Settings** and **Network and
  Provider Settings**, each with a dedicated save action. Scheduled Tasks
  Settings are now also available on Scan with their own save action and stay
  synchronized with Main Settings.
- Renamed **View Tags** to **View and Edit Tags**.

### Test-release notes

- This supersedes `0.1.0.26-test` and remains a public test build, not a stable
  release.

## [0.1.0.26-test] - 2026-07-22

### Added

- Added independent **Save Provider Selections** and **Save Network Selections**
  buttons. Each persists only its own future-scan allow-list and does not scan
  media, create a backup, or modify existing tags.

### Test-release notes

- This supersedes `0.1.0.25-test` and remains a public test build, not a stable
  release.

## [0.1.0.25-test] - 2026-07-22

### Added

- Added **TV Network Streaming Apps** with three administrator-selected modes:
  Network only, Streaming app only, and Both. The plugin never invents a
  Network tag from a streaming-app name.
- Watchmode fallback title details now use Watchmode's actual per-title
  `network_names` separately from its current streaming sources.
- Dashboard-initiated full scans now require an existing tag backup.

### Changed

- Removed the premature automatic provider-family grouping introduced in
  `0.1.0.24-test`. Exact provider variants remain separate unless an
  administrator selectively manages their tags.
- Complete Provider and Network selection lists remain available before scans.

### Test-release notes

- This supersedes `0.1.0.24-test` and remains a public test build, not a stable
  release.

## [0.1.0.24-test] - 2026-07-22

### Added

- Added the optional **Group different types of the same provider** preference.
  It is off by default and groups only documented Netflix, Apple TV, and
  Amazon/Prime variants when enabled; exact source distinctions remain
  available when disabled.

### Fixed

- Provider and network selection sync failures now return and display a safe,
  specific server explanation instead of only “An unknown error occurred.”
- Saving Main Settings now persists the current provider and network checkbox
  selections for future scans.

### Test-release notes

- This supersedes `0.1.0.23-test` and remains a public test build, not a stable
  release.

## [0.1.0.23-test] - 2026-07-22

### Changed

- Removed source-status messages from above the provider and network searches.
- Combined only explicit same-service provider spelling aliases: Apple TV Plus,
  Disney Plus/Disney +, and Discovery Plus/Discovery + now use canonical names.
  Separate storefronts, subscriptions, plan tiers, and profile variants remain
  distinct choices.

### Test-release notes

- This supersedes `0.1.0.22-test` and remains a public test build, not a stable
  release.

## [0.1.0.22-test] - 2026-07-22

### Added

- Added a shared TMDb request gate capped at 35 requests per second.
- TMDb HTTP 429 responses now trigger a temporary plugin-wide cooldown and up
  to two safe read retries, honoring a `Retry-After` response value when one is
  supplied.

### Test-release notes

- This supersedes `0.1.0.21-test` and remains a public test build, not a stable
  release.

## [0.1.0.21-test] - 2026-07-22

### Changed

- Replaced calendar-month Watchmode quota tracking with 30-day cycles anchored
  to the administrator-entered **Quota Resets On** date.
- Added the requested Watchmode reset-date guidance and a visible active-cycle
  status. Provider/network reference-catalog requests now use the same quota
  guard as title lookups.

### Test-release notes

- This supersedes `0.1.0.20-test` and remains a public test build, not a stable
  release.

## [0.1.0.20-test] - 2026-07-22

### Added

- Added **Tag Destination(s)** directly below Backup Settings. New tags can
  now be saved to Jellyfin metadata, NFO files configured by Jellyfin, or both.
- NFO writes use Jellyfin's configured metadata-saver API and stop before a
  scan if any selected library is not configured to save local NFO metadata.

### Changed

- Removed “by mp3li” from the README’s main title.

### Test-release notes

- This supersedes `0.1.0.19-test` and remains a public test build, not a stable
  release.

## [0.1.0.19-test] - 2026-07-22

### Fixed

- Marked the masked API-token fields as one-time codes and excluded them from
  common third-party password managers, so Chrome does not offer to save them
  as a user login when leaving the page.

### Test-release notes

- This supersedes `0.1.0.18-test` and remains a public test build, not a stable
  release.

## [0.1.0.18-test] - 2026-07-22

### Added

- Independent **Select All** and **Select None** controls for both the provider
  and network catalogs.
- Independent provider and network search boxes that filter their own long
  checkbox lists without changing selections.

### Test-release notes

- This supersedes `0.1.0.17-test` and remains a public test build, not a stable
  release.

## [0.1.0.17-test] - 2026-07-22

### Fixed

- Removed Jellyfin's generic plugin-configuration form identity from the custom
  dashboard page. Saving is now an explicit server-wide button action, so
  navigating away cannot invoke a per-user configuration prompt.

### Test-release notes

- This supersedes `0.1.0.16-test` and remains a public test build, not a stable
  release.

## [0.1.0.16-test] - 2026-07-22

### Added

- The requested empty-state guidance in both selection lists when the required
  API credentials have not been saved.

### Test-release notes

- This supersedes `0.1.0.15-test` and remains a public test build, not a stable
  release.

## [0.1.0.15-test] - 2026-07-22

### Changed

- Replaced the combined selection wrapper with two independent, same-level
  sections: **Select Providers** and **Select Networks**.
- Provider choices now load before the first scan from TMDb's movie/TV
  provider catalogs for selected countries and Watchmode's source catalog.
- Network choices now load before the first scan from Watchmode's complete
  TV-network catalog, while retaining names discovered from media scans.

### Test-release notes

- This supersedes `0.1.0.14-test` and remains a public test build, not a stable
  release.

## [0.1.0.14-test] - 2026-07-22

### Added

- Two-column provider and network selection controls that remember all values
  discovered in selected-library scans, even after a cleanup removes current
  tags.
- No-source-lookup synchronization actions for providers and networks. Each
  creates a backup, removes only unselected plugin-owned tags of its own kind,
  and makes future scans honor the chosen list.

### Fixed

- Saved TMDb and Watchmode credentials now use the active Jellyfin theme's
  normal input background instead of a hard-coded dark autofill color.

### Test-release notes

- This supersedes `0.1.0.13-test` and remains a public test build, not a stable
  release.

## [0.1.0.13-test] - 2026-07-22

### Fixed

- Scan ETA now uses a numeric seconds value supplied by the server instead of
  attempting arithmetic on Jellyfin's serialized duration string.
- The live View Tags overview now queries each selected library directly and
  preserves that selected-library identity in its results.

### Added

- Completed scan feedback now remains visible with the checked-item total, the
  number of tags newly added, and the number of media items that received one.

### Test-release notes

- This supersedes `0.1.0.12-test` and remains a public test build, not a stable
  release.

## [0.1.0.12-test] - 2026-07-22

### Added

- Jellyfin-color retro television/question-mark branding: a transparent README
  icon and a larger catalog plugin image served through the repository manifest.

### Test-release notes

- This supersedes `0.1.0.11-test` and remains a public test build, not a stable
  release.

## [0.1.0.11-test] - 2026-07-22

### Added

- **Delete Backup** controls in Main Settings and Scan. They permanently remove
  only the selected stored backup and never alter current Jellyfin tags.
- The complete Backup Settings section to the Scan tab, so a safety snapshot
  can be made immediately before a manual scan.
- Clear country-list guidance when TMDb cannot return its watch-provider regions.

### Changed

- Shortened the dashboard page title to **Media Tagging Manager** and replaced
  its introduction with repository and Patreon links.
- Moved View Tags filters ahead of the Library Overview.
- Expanded and reorganized the public README around the current plugin settings
  tabs and controls.

### Fixed

- Creating a backup before saving a library selection now explains that no
  libraries have been saved instead of showing an unknown error.

### Test-release notes

- This supersedes `0.1.0.10-test` and remains a public test build, not a stable
  release.

## [0.1.0.10-test] - 2026-07-21

### Changed

- Rebuilt the dashboard into **Main Settings**, **View Tags**, and **Scan**
  page-style tabs, with consistent top-level section headers and descriptions.
- Reordered and redesigned backup controls, selected-library configuration, API
  settings, three-country availability selection, tag settings, incoming-media
  settings, scheduled tasks, grouped tag review, and scan controls.
- Removed administrator-configured JSON sources and the configurable parallel
  lookup setting from the product and configuration.
- Shortened the main Dashboard menu label to **Media Tagging Manager** while
  retaining the full catalog title.

### Added

- TMDb-backed availability-country dropdowns with up to three selected regions.
- Watchmode monthly quota settings and visible usage tracking. TMDb is queried
  first; Watchmode is only used as a quota-limited provider fallback.
- A Stop Scan action, selected-library list on the Scan tab, backup dropdown,
  disabled no-backup restore action, and staged manual tag edits saved from the
  View Tags tab.

### Test-release notes

- This supersedes `0.1.0.9-test` and remains a public test build, not a stable
  release.

## [0.1.0.9-test] - 2026-07-21

### Fixed

- Replaced the incorrect dashboard response-wrapper workaround with Jellyfin Web
  10.11.11's documented JSON-read pattern: `ApiClient.getJSON()` for every
  plugin GET endpoint. This parses the actual settings response before reading
  its configuration and selectable libraries.
- Shortened the Dashboard plugin-menu label to **Media Tagging Manager** while
  retaining the full catalog and page title.

### Test-release notes

- This supersedes `0.1.0.8-test` and remains a public test build, not a stable
  release.

## [0.1.0.8-test] - 2026-07-21

### Attempted fix

- Added a response-wrapper workaround for the 10.11.11 dashboard client. Live
  testing showed it did not parse `ApiClient.ajax()`'s returned `Response`.
  The precise `ApiClient.getJSON()` correction is in `0.1.0.9-test`.

### Test-release notes

- This supersedes the unuploaded `0.1.0.7-test` package and remains a public
  test build, not a stable release.

## [0.1.0.7-test] - 2026-07-21

### Fixed

- Added the supported Dashboard plugin-menu entry, using the same
  `EnableInMainMenu` page setting as File Transformation.
- Moved dashboard scan requests into Jellyfin's scheduled-task manager instead
  of using detached background tasks.
- Sent Watchmode's documented region filter and added the required JustWatch
  attribution for TMDb watch-provider data.
- Preserved managed tags when an enabled source cannot identify or query an
  item, restricted manual edits to selected Movie/Series libraries, and
  serialized backup creation/restoration with scans and manual edits.

### Documentation

- Added the source-level Jellyfin 10.11.11 compatibility audit.

### Test-release notes

- This was a local test package superseded before publication by
  `0.1.0.8-test`.

## [0.1.0.6-test] - 2026-07-21

### Fixed

- Matched Jellyfin 10.11.11's own library controller by retrieving all virtual
  folders through `ILibraryManager.GetVirtualFolders(true)`.
- Made the dashboard accept either PascalCase or camelCase API response fields
  and display a readable response error instead of silently leaving library
  selection blank.

### Test-release notes

- This supersedes the `0.1.0.5-test` catalog build and remains a public test
  build, not a stable release.

## [0.1.0.5-test] - 2026-07-21

### Fixed

- Removed the dashboard-internal configuration and virtual-folder requests that
  could leave the settings page in an endless global loading state.
- Added administrator-only plugin endpoints that retrieve selectable Jellyfin
  libraries and save plugin settings directly through Jellyfin server services.

### Test-release notes

- This supersedes the `0.1.0.4-test` catalog build and remains a public test
  build, not a stable release.

## [0.1.0.4-test] - 2026-07-21

### Changed

- Rebuilt the plugin against the official Jellyfin.Controller and Jellyfin.Model
  `10.11.11` packages for the active Jellyfin 10.11.11 test server.
- Updated the test catalog ABI declaration to `10.11.11.0`.

### Test-release notes

- This supersedes the `0.1.0.3-test` catalog build and remains a public test
  build, not a stable release.

## [0.1.0.3-test] - 2026-07-21

### Fixed

- Load plugin settings and Jellyfin libraries immediately when the dashboard
  configuration page opens, avoiding dashboard variants that do not emit the
  expected page-show event.

### Changed

- Replaced the Scan, Settings & sources, and Library overview tabs with one
  continuous, scrollable administrator page.

### Test-release notes

- This supersedes the `0.1.0.2-test` catalog build and remains a public test
  build, not a stable release.

## [0.1.0.2-test] - 2026-07-21

### Fixed

- Corrected the Jellyfin dashboard lifecycle event so the plugin now loads the
  selectable library list, saved configuration, scan controls, and backup list.
- Added visible errors for failed settings loads, saves, manual backup creation,
  and Undo so an unsuccessful request no longer appears to do nothing.
- Clarified that a library must be selected and saved before a manual backup can
  be created.

### Test-release notes

- This supersedes the `0.1.0.1-test` catalog build and remains a public test
  build, not a stable release.

## [0.1.0.1-test] - 2026-07-21

### Added

- Jellyfin plugin foundation targeting Jellyfin 10.11.3.
- Administrator dashboard for library selection, tag behavior, region, source configuration, scans, filtering, and manual corrections.
- Explicit overlap-safe Jellyfin tags: `Provider: <name>` and `Network: <name>`.
- TMDb availability/network adapter, Watchmode availability adapter, and configurable custom JSON source adapter.
- Per-library and all-selected-library manual scans.
- Scan status with active title, completed count, progress percentage, and estimated remaining time.
- Native scheduled full-refresh task with an administrator-configurable interval.
- Optional incoming-media checks after Jellyfin library scans; disabled by default and independent from full refreshes.
- Complete selected-library tag backups before tag-changing scans, incoming-media updates, and manual tag edits.
- Named backup creation, backup list, per-backup restore, and Undo last tag operation in plugin settings.
- Safeguards that preserve existing plugin-managed tags when no source is configured or all enabled sources fail.
- Documentation/API_KEYS.md with per-server credential setup, safe rotation guidance, and a non-secret custom JSON example.
- Documentation/project-goals.txt for product-scope and delivery tracking.
- Initial license and package metadata for the public source repository.
- A compact README TMDb credential walkthrough, truthful application-form wording, and visible TMDb attribution in the plugin settings.

### Changed

- Renamed the project and displayed plugin name to Media Tagging Manager Jellyfin Plugin.
- Renamed the plugin assembly and source namespace to `Jellyfin.Plugin.MediaTaggingManager`.
- Moved built-in source credentials from request URLs to HTTP headers.
- Improved dashboard provider and network filters to support partial-text matching.

### Test-release notes

- This is a public, catalog-install test build—not a stable release.
- The test manifest points to a real ZIP for Jellyfin 10.11.3 and includes that ZIP's real checksum.
- The ZIP, manifest, and repository contain no administrator API keys, Jellyfin configuration, backups, logs, or media data.
- Runtime testing in an installed Jellyfin server remains required before the first stable release.
