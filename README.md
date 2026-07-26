<p align="center">
  <img src="Assets/Branding/media-tagging-manager-icon.png" alt="Media Tagging Manager icon: retro television with a question mark" width="180" />
</p>

<h1 align="center">Media Tagging Manager Jellyfin Plugin</h1>

<p align="center">
  <strong>⚠️ Testing build:</strong> actively tested on Jellyfin 10.11.11; not yet a stable v1 release.
</p>

<p align="center">
  A Jellyfin metadata companion for organizing the media you already own with current streaming providers, television networks, genres, keywords, collections, cast and crew, production information, ratings, languages, and reusable TMDb relationship data.
</p>

<p align="center">
  <img alt="Status: Active testing" src="Assets/Badges/status.svg" />
  <img alt="Platform: Jellyfin 10.11.11" src="Assets/Badges/platform.svg" />
  <img alt="Interface: Jellyfin Dashboard" src="Assets/Badges/interface.svg" />
  <img alt="Tag types: 5" src="Assets/Badges/tag-types.svg" />
  <img alt="Sources: TMDb and Watchmode" src="Assets/Badges/sources.svg" />
  <img alt="Refresh: Manual or scheduled" src="Assets/Badges/refresh.svg" />
</p>

## What it does

Media Tagging Manager reads only the Jellyfin libraries you explicitly select.
For supported Movies and Series, it can enrich or organize metadata from TMDb
and, where appropriate, Watchmode. Episodes inherit their series context.

It is designed for a library owner who wants answers to questions like:

- “Where can I stream this title in my country right now?”
- “Which television network is this series associated with?”
- “Which titles in my library share a genre, keyword, collection, cast member,
  production company, language, or TMDb relationship?”

The plugin does not download media, rename files, alter video/audio streams,
or bypass any streaming service’s access controls.

## Jellyfin tag types

These are the five prefixed Jellyfin tag types the plugin can add. They remain
distinct so that one title can have more than one kind of information without
mixing their meanings.

| Tag type | Example | Meaning |
| --- | --- | --- |
| Provider | `Provider: Netflix` | A current streaming provider, rental service, purchase service, or free viewing service where the title is available in one of your selected regions. |
| Network | `Network: BBC One` | A title-level television network or original distributor returned by TMDb or Watchmode. The plugin never invents this from a streaming-app name. |
| Genre | `Genre: Drama` | A broad TMDb category selected in Genres and Keywords Settings. |
| Keyword | `Keyword: Time Travel` | A specific TMDb descriptive term selected through the Keywords setting. |
| Collection | `Collection: The Hunger Games Collection` | A direct TMDb movie-collection membership that you reviewed and chose to add. |

## Current testing status

The current catalog build is **0.1.0.65-test**. The core provider/network,
genre/keyword, collection, cast/crew, people-photo, More Like This, production,
ratings, and language workflows have been exercised on a real Jellyfin 10.11.11
server. Remaining release checks and their recorded results live in
[Documentation/goal-testing.txt](Documentation/goal-testing.txt).

This is still a test release. Create a tag backup before a broad scan and keep
your usual server backups.

## Install

1. Open **Dashboard → Plugins → Repositories** in Jellyfin.
2. Add this repository manifest URL:

   ```text
   https://raw.githubusercontent.com/mp3li/Media-Tagging-Manager-Jellyfin-Plugin/main/manifest.json
   ```

3. Refresh the catalog, open **Media Tagging Manager Jellyfin Plugin**, and
   install the newest test version.
4. Restart Jellyfin if it asks you to.
5. Open **Dashboard → Media Tagging Manager** from the Dashboard sidebar.

The manifest and release ZIP contain no API keys, server configuration,
backups, logs, cached source data, NFO files, or media.

## Quick start

1. In **Main Settings**, select one small test library and save.
2. Add your TMDb API Read Access Token in **API Settings** and save it.
3. Optionally add your Watchmode API key in **API Settings**, set its request
   limit and quota-reset date, and save. It is used only as the quota-tracked
   fallback when TMDb does not return the needed result.
4. In **Network and Provider Settings**, choose availability regions, load
   Networks if needed, choose the Providers and Networks you want, and save.
5. Optionally configure the other metadata tabs described below.
6. Create a tag backup in **Main Settings** or **Scan**.
7. Use **Scan All Selected Libraries**.
8. Review **Additions and Removals in the Last Scan**, then review Provider and
   Network tags at the bottom of **Network and Provider Settings**.

Each tab’s Save button saves only that tab’s settings. Save a tab before using
one of its load, sync, or scan actions.

## Sources and credentials

| Source | Used for | Credential |
| --- | --- | --- |
| [TMDb](https://www.themoviedb.org/) | Regional availability, title-level TV networks, genres, keywords, direct TMDb collections, cast/crew, people photos, recommendations, similar titles, production data, ratings, certifications, languages, and translations | API **Read Access Token** |
| [Watchmode](https://www.watchmode.com/) | Quota-tracked fallback availability/network lookup when TMDb does not return the requested result | API key |

TMDb is checked first. Watchmode is not used as a second copy of every match;
it is the fallback path for the applicable missing result and respects the
configured 30-day request limit. Availability is region-dependent; choose up
to three availability countries.

More documented, authorized sources may be added in a future iteration after
their matching quality, terms, coverage, and limits are reviewed.

### Get your keys

<details>
<summary><strong>Get a TMDb API Read Access Token and Watchmode API Key — usually only takes a few minutes</strong></summary>

<br />

For TMDb, create an application from **Account settings → API**. If the form
asks for application details for a genuinely personal Jellyfin server, these
values are suitable:

| Field | Suggested value |
| --- | --- |
| Application name | `Media Tagging Manager Jellyfin Plugin` |
| Application URL | `https://github.com/mp3li/Media-Tagging-Manager-Jellyfin-Plugin` |
| Description | `A self-hosted Jellyfin plugin for my personal media library. It uses the TMDb API to organize metadata for titles already in my library. Each administrator supplies their own private TMDb API Read Access Token in their Jellyfin plugin settings.` |

Copy the **API Read Access Token**, not TMDb’s older API-key value. Paste it
only into **Dashboard → Media Tagging Manager → Main Settings → API Settings**.

For Watchmode, create an account and obtain a key from its account dashboard.
Enter its request allowance, **Quota Resets On** date, and, if necessary, the
current usage already shown in your Watchmode account. The plugin adds its
future tracked requests to that starting number.

</details>

Never place credentials in GitHub, screenshots, shared settings exports, or
release archives. See [API key setup and rotation](Documentation/API_KEYS.md)
for the full server-administrator guide.

## Data and safety model

### Jellyfin tags

The plugin writes clearly prefixed normal Jellyfin tags:

```text
Provider: Netflix
Network: BBC One
Genre: Drama
Keyword: Time Travel
Collection: The Hunger Games Collection
```

Provider means current viewing availability. Network means actual title-level
television network/distributor metadata returned by a source; the plugin does
not invent a Network tag from an app name. Both may apply to the same title.

Your unrelated Jellyfin tags are never removed. The provider/network replacement
option removes only the plugin’s own outdated Provider and Network tags; genre,
keyword, collection, and unrelated tags have their own controls and are not
silently cleared by that setting.

### Native Jellyfin metadata

Where Jellyfin has a supported field, the plugin uses Jellyfin’s normal
metadata-update path:

- Cast, crew, and shared people images
- Studios / production companies
- Production countries
- Community Rating
- One Official Rating, chosen through **Primary Jellyfin Classification Country**

If the library itself is configured in Jellyfin to save NFO metadata, Jellyfin
decides whether these ordinary metadata updates are written to NFO files. This
plugin does not write NFO files separately or write into media files.

### Plugin-owned supplemental data

Some TMDb data has no equivalent multi-value Jellyfin field. The plugin keeps
these values in its own server-side data for its dashboard and future compatible
plugins:

- Recommendations and Similar Titles
- Vote counts, all selected-country classifications, and adult flags
- Original language, spoken languages, and available translations
- Latest-operation addition/removal overlays
- Source-logo and optional poster caches

Removing these records does not alter media files. Cleanup buttons say exactly
which category they remove.

### Backups, undo, and scoped access

Backups capture the **complete current tag list** for every item in selected
libraries. Restore intentionally overwrites the current tags on the saved
items, including unrelated tags that existed when the backup was made. Use it
carefully.

Empty library selection means **no libraries**, never every library. All scans,
loads, manual edits, and cleanup actions are restricted to the saved selection.

## Dashboard guide

The dashboard uses a primary tab row plus a horizontally scrollable secondary
row. Tab labels intentionally stay on one line.

### Main Settings

- **Backup Settings** — create, restore, delete, and undo complete tag
  snapshots.
- **Select Libraries** — choose the only libraries the plugin can read or
  change.
- **API Settings** — TMDb/Watchmode credentials, Watchmode cycle limit/reset
  date/current usage, and safe credential saving.
- **Logo Settings** — enable or disable logo use, set a bounded cache limit,
  load all/selected logos, selectively delete logos, or clear the cache.
- **Newly Added Media Settings** — optionally check new supported media after
  ordinary Jellyfin library scans.
- **Scheduled Tasks** — enable a full refresh interval and optionally remove
  stale plugin-created Provider/Network tags.

### Network and Provider Settings

- **Availability Region Settings** — choose up to three countries for current
  streaming availability.
- **Tag Settings** — enable Provider tags, Network tags, or both.
- **TV Network Streaming Apps Settings** — choose whether an API-returned
  network app result becomes the network, the provider, or both.
- **Select Providers / Select Networks** — independent searchable pickers with
  Select All/None, individual saves, logos, and sync actions. Network lists are
  loaded on demand and filtered by saved availability regions.
- **Library Overview** — filter, review, and manually edit Provider and Network
  tags. Its library groups are collapsible.
- **Unknown Providers and Networks** — review prefixed Provider/Network tags in
  selected libraries that the plugin did not create and its enabled sources do
  not recognize. You can inspect matching items, assign an official name, and
  upload one logo without rewriting the existing tags.

Use the sync actions only when you want to remove plugin-created Provider or
Network tags outside the saved picker selection. They do not contact sources.

### Genres and Keywords Settings

- Choose the TMDb genres allowed during future scans.
- Enable or disable TMDb keyword tags.
- Save genre choices independently, sync existing genre tags to the selection,
  or remove keywords created by this plugin.
- Review and manually edit Genre and Keyword tags in the dedicated collapsible
  **Genres and Keywords Library Overview**.

### Collections Tags Settings

Scan selected libraries for direct TMDb movie collection membership, review the
matches grouped by library, select the ones you want, and add only those
`Collection:` tags. The plugin does not guess franchises or collections.

### Scan

Create a backup, start a full selected-library scan, watch progress and ETA,
request a stop, or undo the last tag action. **Additions and Removals in the
Last Scan** is a collapsible, editable, per-library review that colors new
values and removals; both colors are configurable.

The **Scan** tab is the last tab on the secondary row. It repeats Backup
Settings and Scheduled Tasks Settings intentionally so they are available
immediately before a broad scan.

### Cast and Crew Settings

Use this tab to preserve and fill people metadata rather than overwrite it.

- Add missing cast with an optional maximum total count.
- Add missing selected-job crew.
- Fill missing cast and crew photos.
- Run a dedicated photo-only scan for people already attached to selected
  media.
- Review current cast, crew, and people photos in a collapsible overview with
  latest-operation colors.
- Remove only cast, crew, or images that the plugin’s private ownership record
  says it created.

People photos are Jellyfin server metadata shared across titles, not one new
copy per title.

### More Like This Settings

Enable TMDb **Recommendations** and/or **Similar Titles** for selected-library
Movies and Series. These are title-to-title TMDb relationships, not
personalized “Because You Watched” results.

Choose whether to store poster links, optional poster files in a bounded cache,
or both. Use **Load** for missing records and **Update** to recheck saved
records. The collapsed per-library review paginates source media items so large
libraries stay responsive.

### Production Companies and Countries Settings

Add missing native Jellyfin Studio and production-country data returned by TMDb
without replacing existing values. Choose production countries independently of
availability regions, optionally cache company logos, use the dedicated load,
review collapsible additions/removals, edit an item’s production data, or remove
only values this plugin recorded as adding.

### Ratings Settings

Enable any combination of:

- TMDb Community Rating
- TMDb Vote Count
- TMDb country-specific Age Ratings and Classifications
- TMDb adult-content flag

**Primary Jellyfin Classification Country** starts blank and lists every
available country. Choosing it sets the one country whose certification is
written to Jellyfin’s native Official Rating field and automatically retains
that country in the saved classification selection.

Use **Load Ratings and Classifications** for this category alone. **Sync with
Only Selected Age Ratings and Classifications** removes plugin-retained country
classifications outside the saved selection; it does not fetch TMDb or remove
unrelated Jellyfin metadata. The overview is collapsed by default, grouped by
library, and has configurable addition/removal colors.

### Spoken Languages and Translations Settings

Save any combination of TMDb original language, spoken languages, and available
translations for selected-library Movies and Series. There is no language
filter: each enabled setting retains every value TMDb returns.

Use the dedicated load action or enable the options for full scans. The
collapsible overview shows original language, spoken languages, and translations
per selected library. Cleanup is deliberately separate: one button removes only
spoken languages and another removes only translations added by this plugin.

## Compatibility and limits

- Target server version: **Jellyfin 10.11.11**.
- Current supported scan item kinds: **Movies and Series**. Selecting another
  library type does not make unsupported media kinds taggable.
- TMDb/Watchmode coverage, identifiers, regional availability, logos, and
  response content are source-dependent.
- A title without the identifier a source needs is skipped for that source;
  the plugin does not invent results.
- Ratings, classifications, languages, and translations require TMDb.
- The dashboard is administrator-only. Treat your Jellyfin server, plugin data
  directory, and credentials as sensitive.

Read the detailed [Jellyfin 10.11.11 compatibility audit](Documentation/JELLYFIN_10.11.11_COMPATIBILITY_AUDIT.md) and the [testing tracker](Documentation/goal-testing.txt) before calling a release stable.

## Documentation

- [API keys and source setup](Documentation/API_KEYS.md)
- [Changelog](Documentation/CHANGELOG.md)
- [Jellyfin 10.11.11 compatibility audit](Documentation/JELLYFIN_10.11.11_COMPATIBILITY_AUDIT.md)
- [Project goals and acceptance tracker](Documentation/project-goals.txt)
- [Testing and validation tracker](Documentation/goal-testing.txt)

## License

This repository is currently distributed under the license in
[LICENSE](LICENSE). Read it before redistributing, modifying, or using the
project outside your own server.
