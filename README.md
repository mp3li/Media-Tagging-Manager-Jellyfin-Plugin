<p align="center">
  <img src="Assets/Branding/media-tagging-manager-icon.png" alt="Media Tagging Manager icon: retro television with a question mark" width="180" />
</p>

<h1 align="center">Media Tagging Manager Jellyfin Plugin by mp3li</h1>

<p align="center">
  <strong>⚠️ Testing build:</strong> This plugin is still under active testing and is not yet a stable release.
</p>

<p align="center">
  A Jellyfin server plugin that checks selected libraries against enabled availability sources, then adds clear, overlap-friendly provider and network tags directly to the titles you already own.
</p>

<p align="center">
  <img alt="Status: In Active Development" src="Assets/Badges/status.svg" />
  <img alt="Platform: Jellyfin 10.11.11" src="Assets/Badges/platform.svg" />
  <img alt="Interface: Jellyfin Dashboard" src="Assets/Badges/interface.svg" />
  <img alt="Tags: Providers and Networks" src="Assets/Badges/tags.svg" />
  <img alt="Sources: TMDb and Watchmode" src="Assets/Badges/sources.svg" />
  <img alt="Refresh: Manual or Scheduled" src="Assets/Badges/refresh.svg" />
</p>

## Table of Contents

<details>
<summary>Open Table of Contents</summary>

<br />

- [About the Project](#about-the-project)
- [How Tags Work](#how-tags-work)
- [Source Coverage](#source-coverage)
- [Requirements](#requirements)
- [Installation](#installation)
- [Plugin Settings](#plugin-settings)
- [Project Structure](#project-structure)
- [Known Limitations](#known-limitations)
- [Responsible Use and Availability Disclaimer](#responsible-use-and-availability-disclaimer)
- [License](#license)

</details>

## About the Project

Media Tagging Manager Jellyfin Plugin is for media-library owners who want their local collection tagged with the online streaming providers a title is currently available to watch on, and with the television network it belongs to.

It works with the existing Jellyfin library. It does not download media, alter filenames, replace ordinary Jellyfin tags, or assume that a title has only one provider or network.

## How Tags Work

The plugin writes normal Jellyfin tags with explicit prefixes:

```text
Provider: Netflix
Provider: Prime Video
Network: BBC One
```

A service where a title can currently be watched is not necessarily the network that originally aired or carries it. A title may have any number of provider tags and network tags at once.

When the setting to remove outdated plugin-assigned tags is enabled, the plugin replaces only tags beginning with `Provider: ` or `Network: `. Genres, studios, collections, and unrelated custom Jellyfin tags remain untouched. If every enabled source is unavailable for a title, existing plugin tags are preserved rather than erased.

## Source Coverage

| Source | What it contributes | Credentials | Status |
| --- | --- | --- | --- |
| TMDb | Regional streaming providers and TV-network metadata | TMDb API Read Access Token | Built in; checked first |
| Watchmode | Quota-tracked fallback regional streaming availability when TMDb finds no provider | Watchmode API key | Built in |
| Streaming Availability API | Additional streaming-availability coverage | Streaming Availability API key | Planned source |

Availability can be selected for up to three countries. A provider available in one country may be unavailable in another.

## Requirements

- **Jellyfin 10.11.11** — the current plugin build targets this Jellyfin version.
- **Jellyfin administrator access** — required for plugin settings, scans, backups, and manual tag edits.
- **Internet access from the Jellyfin server** — only for the sources you enable.
- **At least one credential from the two built-in sources** — a TMDb API Read Access Token or a Watchmode API Key.

## Installation

1. In Jellyfin, open **Dashboard → Plugins → Repositories**.
2. Add the repository URL below, then refresh the plugin catalog:

   ```text
   https://raw.githubusercontent.com/mp3li/Media-Tagging-Manager-Jellyfin-Plugin/main/manifest.json
   ```

3. Find **Media Tagging Manager Jellyfin Plugin** in the catalog and select **Install**.
4. Restart Jellyfin when prompted.
5. Open **Dashboard → Media Tagging Manager** to configure the plugin.

The repository manifest contains plugin-release information only. It never contains API keys, Jellyfin configuration, backups, logs, or media data. Each server administrator adds their own source credentials in the plugin settings after installation.

## Plugin Settings

The plugin is configured from **Dashboard → Media Tagging Manager**. Save changes on the **Main Settings** tab before using the other tabs.

### Main Settings

#### Backup Settings

Create a complete tag backup for every item in the selected libraries before making changes. A backup captures the entire current tag list for each item, including tags that were not created by this plugin.

- **Create Tag Backup** creates a named snapshot.
- **Undo Last Tag Action** restores the newest available backup.
- **Available Backups** lists saved backups with their date, time, and item count.
- **Restore from Backup** restores the selected snapshot and overwrites the current tags for its saved items.
- **Delete Backup** permanently removes the selected backup without changing current Jellyfin tags.

Backups remain in Jellyfin’s plugin data directory through normal server restarts. Restore is intentionally powerful: it also restores unrelated custom tags to their state at the time of the backup.

#### Select Libraries

Choose the Jellyfin libraries the plugin may access. Only selected libraries are read or changed, regardless of what you named them. Within those libraries, the plugin tags Movies and Series; episodes inherit their series context.

#### API Settings

Enter API credentials for the sources you want to use. Credentials are stored in that Jellyfin server’s plugin configuration; never share them or include them in GitHub, screenshots, releases, or backups.

- **TMDb API Read Access Token** is the primary source for regional streaming providers and television networks.
- **Watchmode API Key** is an optional fallback for a title where TMDb returns no provider. It requires an IMDb ID.
- **Watchmode monthly request limit** is a safety cap. The plugin tracks its own usage and stops sending Watchmode requests when that cap is reached.

The dashboard sends built-in-source credentials in request headers, not in source URLs. Jellyfin’s plugin configuration should still be treated as sensitive server data; protect access to the Jellyfin dashboard, its data directory, backups, and logs. The plugin does not claim to encrypt API keys at rest.

For detailed setup and key rotation, read [API_KEYS.md](Documentation/API_KEYS.md).

<details>
<summary><strong>Get a TMDb API Read Access Token and Watchmode API Key — usually only takes a few minutes</strong></summary>

<br />

TMDb is the easiest built-in source to begin with because it provides both regional watch-provider information and TV-network metadata. Create or sign in to a TMDb account, then open **Account settings → API** and create an application.

For a self-hosted Jellyfin server used only for your own library, choose the **personal** or **non-commercial** option if that truthfully describes your use. Do not select a personal option for a commercial project; review TMDb's terms or contact TMDb instead.

If the form asks for application information, these truthful values are appropriate for a personal server:

| Form field | Suggested value |
| --- | --- |
| Application name | `Media Tagging Manager Jellyfin Plugin` |
| Application URL | `https://github.com/mp3li/Media-Tagging-Manager-Jellyfin-Plugin` |
| Application summary / description | `A self-hosted Jellyfin plugin for my personal media library. It uses the TMDb API to identify regional watch providers and television networks for titles already in my library. Each server administrator supplies and stores their own private TMDb API Read Access Token in that server's plugin settings. The plugin does not distribute, share, or expose TMDb API credentials.` |

After TMDb approves the application, copy the **API Read Access Token**—not the older API-key value—and paste it only into **Dashboard → Media Tagging Manager → Main Settings → API Settings**. Never put the token in a GitHub issue, screenshot, README, release archive, or this repository.

For Watchmode, create an account and generate an API key through its account dashboard. Paste it only into the Watchmode field in the same API Settings section. You may use either TMDb or Watchmode, but using both gives the plugin its configured fallback path.

</details>

#### Availability Region Settings

Choose up to three countries used for streaming-availability results. The country choices come from TMDb’s watch-provider regions. Save a valid TMDb API Read Access Token, then reload the page if the complete country list does not appear.

#### Tag Settings

Choose whether to create **Provider tags**, **Network tags**, or both.

- Provider tags identify streaming, rental, purchase, ad-supported, or free services where a title is currently available.
- Network tags identify a television series’ network.

Your existing Jellyfin tags added without this plugin are never removed. The plugin only adds new tags and, if you enable removal of outdated tags, only removes tags that it added.

#### Select Providers

This independent settings section loads the complete movie and TV
watch-provider catalogs from TMDb for the selected countries, plus Watchmode's
provider catalog when its key is configured. That means providers can be chosen
before the first media scan. Previously discovered provider values remain
listed too.

Use **Sync with Only Selected Providers** when your selected libraries already
have more provider tags than you want. It creates a backup, deletes provider
tags for unselected providers, does not remove tags for your chosen providers,
and saves the choice for future scans. It does not contact any source and never
changes network or unrelated Jellyfin tags.

#### Select Networks

This separate settings section loads Watchmode's complete TV-network catalog
when its key is configured, plus networks previously discovered by the plugin.
It therefore supports choosing network names before the first media scan.

Use **Sync with Only Selected Networks** when your selected libraries already
have more network tags than you want. It creates a backup, deletes network tags
for unselected networks, does not remove tags for your chosen networks, and
saves the choice for future scans. It does not contact any source and never
changes provider or unrelated Jellyfin tags.

#### Newly Added Media Settings

Turn on **Scan newly added media in my libraries using this plugin** to check newly added Movies and Series after a normal Jellyfin library scan. Turn it off to prevent automatic API checks for incoming media; manual and scheduled full scans remain available.

The first time this feature is enabled, the plugin records a starting point instead of rechecking the entire existing library. Use a manual full scan for existing titles.

#### Scheduled Tasks

Turn on the scheduled task and choose a refresh interval in hours to keep provider and network information current. The task also appears as **Refresh provider and network tags** under **Dashboard → Scheduled Tasks**.

Enable the setting to remove outdated plugin-assigned tags if you want a later scan to remove a tag when a provider or network no longer hosts a title. Leaving it off preserves old plugin-created tags. Other Jellyfin tags remain untouched.

### View Tags

Use **Filters** at the top of the tab to narrow the selected-library results by provider, network, tagged state, or a provider-and-network combination.

The **Library Overview** groups matching Movies and Series by selected library. Use **Edit** to manually replace a title’s plugin-owned provider and network tags, then use **Save Tag Changes** to apply the edits. A backup is created before tag changes are written.

### Scan

The Scan tab lists the libraries currently selected in Main Settings and lets you initiate a full scan for all of them.

- **Scan All Selected Libraries** checks every selected library.
- **Stop Scan** requests cancellation of the current dashboard-initiated scan.
- The status area shows the active title, completed and total counts, progress percentage, and an estimated remaining time. When it finishes, it retains a summary of checked items, new tags, and tagged media items.
- The Backup Settings section provides the same create, undo, restore, and delete controls as Main Settings, so you can create a safety backup immediately before scanning.

Only Movies and Series are automatically queried. Episodes inherit their series-level availability context rather than triggering a duplicate scan for every episode.

## Project Structure

```text
Media Tagging Manager/
├── Api/                    protected dashboard endpoints
├── Configuration/          per-server plugin settings
├── Models/                 tag and scan dashboard models
├── ScheduledTasks/         Jellyfin refresh task
├── Services/               source adapters, scanner, tag rules, and progress state
├── Web/                    embedded Jellyfin dashboard page
├── Plugin.cs               plugin identity and dashboard registration
└── ServiceRegistrator.cs   Jellyfin dependency-injection registration
Documentation/              project documentation and trackers
├── API_KEYS.md             administrator credential setup guide
├── CHANGELOG.md            release notes
├── goal-testing.txt        live-server testing checklist and results log
└── project-goals.txt       product goals and acceptance behavior
README.md                   project overview and setup guide
```

## Known Limitations

- Current automatic querying is limited to **Movies and Series**. Music, books, episodes, and other Jellyfin item kinds are not source adapters in this version.
- Availability depends on region, external IDs, API coverage, source plans, and source uptime. Missing data is not proof that a title is unavailable.
- TMDb TV networks describe TV-network metadata; they are not a universal network authority for every media type.
- This plugin does not ship third-party API credentials and does not bypass provider logins, DRM, access restrictions, source rate limits, or terms of use.

## Responsible Use and Availability Disclaimer

This plugin organizes availability and network information for media already in a Jellyfin library. It does not provide media, unlock services, scrape account-only content, or guarantee that a provider listing is current, complete, or available to every user.

Use only your own API accounts and sources you are allowed to query. Review each source's terms, attribution requirements, rate limits, regional restrictions, and data-retention rules. Streaming catalog data changes often; treat a tag as a helpful library classification and verify important availability decisions with the provider itself.

## License

Media Tagging Manager Jellyfin Plugin by mp3li is licensed under the [GNU General Public License v3.0 or later](LICENSE) (`GPL-3.0-or-later`).
