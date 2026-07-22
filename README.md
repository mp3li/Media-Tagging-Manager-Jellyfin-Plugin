<h1 align="center">Media Tagging Manager Jellyfin Plugin by mp3li</h1>

<p align="center">
  <strong>⚠️ Testing build:</strong> This plugin is still under active testing and is not yet a stable release. Please test it on a backed-up library and report any unexpected behavior before relying on it for regular automated scans.
</p>

<p align="center">
  A Jellyfin server plugin that checks selected movie and TV libraries against enabled availability sources, then adds clear, overlap-friendly provider and network tags directly to the titles you already own.
</p>

<p align="center">
  <img alt="Status: In Active Development" src="Assets/Badges/status.svg" />
  <img alt="Platform: Jellyfin 10.11.11" src="Assets/Badges/platform.svg" />
  <img alt="Interface: Jellyfin Dashboard" src="Assets/Badges/interface.svg" />
  <img alt="Tags: Providers and Networks" src="Assets/Badges/tags.svg" />
  <img alt="Sources: TMDb, Watchmode, and Custom JSON" src="Assets/Badges/sources.svg" />
  <img alt="Refresh: Manual or Scheduled" src="Assets/Badges/refresh.svg" />
</p>

## Table of Contents

<details>
<summary>Open Table of Contents</summary>

<br />

- [About the Project](#about-the-project)
- [What the Plugin Does](#what-the-plugin-does)
- [How Tags Work](#how-tags-work)
- [Source Coverage](#source-coverage)
- [API Keys and Server Privacy](#api-keys-and-server-privacy)
- [Requirements](#requirements)
- [Build and Install](#build-and-install)
- [Repository Manifest and API Keys](#repository-manifest-and-api-keys)
- [Test Prerelease Catalog](#test-prerelease-catalog)
- [First-Time Setup](#first-time-setup)
- [Scanning and Progress](#scanning-and-progress)
- [Library Overview and Manual Edits](#library-overview-and-manual-edits)
- [Safety Backups and Undo](#safety-backups-and-undo)
- [Automatic Refresh](#automatic-refresh)
- [Custom JSON Sources](#custom-json-sources)
- [Project Structure](#project-structure)
- [Known Limitations](#known-limitations)
- [Responsible Use and Availability Disclaimer](#responsible-use-and-availability-disclaimer)
- [License](#license)

</details>

## About the Project

Media Tagging Manager Jellyfin Plugin by mp3li is for media-library owners who want their local collection tagged with the online streaming providers a title is currently available to watch on and with the television network it belongs to, without treating those two ideas as the same thing.

The plugin is built around a practical Jellyfin workflow:

- choose exactly which Jellyfin libraries it may access
- connect one or more availability sources with the server administrator's own API credentials
- run a full selected-library scan or scan one library from the dashboard
- filter the resulting title list by any provider, network, or combination of both
- correct a title manually when you know the source data needs adjustment
- re-check on a schedule when streaming availability changes

It works with the existing Jellyfin library. It does not download media, alter filenames, replace ordinary Jellyfin tags, or assume that a title has only one provider or network.

## What the Plugin Does

- Lets an administrator select the movie and TV libraries the plugin may scan.
- Writes provider tags, network tags, or both, based on the configured mode.
- Adds multiple classifications when they overlap.
- Uses TMDb for regional watch-provider data and TV-network metadata when configured.
- Uses Watchmode as an optional additional availability source when configured.
- Accepts administrator-configured JSON endpoints for additional licensed sources.
- Provides a **Scan this library** button for every selected library and a **Scan all selected libraries** action.
- Shows the item currently being checked, completed count, percentage, and an estimated remaining time while a scan runs.
- Adds a **Library overview** dashboard tab for filtering selected-library titles by provider, network, tagged state, or a provider-plus-network combination.
- Lets an administrator replace a title's plugin-owned provider and network tags manually.
- Adds a native Jellyfin scheduled task named **Refresh provider and network tags**.
- Can independently check new incoming movies and TV series after a normal Jellyfin library scan, or leave that behavior off.

## How Tags Work

The plugin writes normal Jellyfin tags with explicit prefixes:

```text
Provider: Netflix
Provider: Prime Video
Network: BBC One
```

This is intentional. A service where a title can currently be watched is not necessarily the network that originally aired or carries it. A title may have any number of provider tags and network tags at once.

When **Replace tags managed by this plugin** is enabled, the plugin replaces only tags beginning with `Provider: ` or `Network: `. Your genres, studios, collections, and unrelated custom Jellyfin tags remain untouched. If every enabled source is unavailable for a title, existing plugin tags are preserved rather than erased.

## Source Coverage

| Source | What it contributes | Credentials | Current status |
| --- | --- | --- | --- |
| TMDb | Regional streaming providers; TV networks | TMDb API Read Access Token | Built in |
| Watchmode | Additional regional streaming sources | Watchmode API key | Built in |
| Custom JSON | Provider and/or network names from a compatible endpoint | Defined per source | Built in |
| Streaming Availability API | Strong candidate for future first-class integration, with catalog coverage across supported countries | Its own API key | Not bundled yet |
| TVMaze / TheTVDB | Potential network and TV-metadata fallbacks, subject to identifier coverage and API terms | Varies | Not bundled yet |

TMDb and Watchmode should be viewed as complementary, not as guarantees that every service in every country will be present. The configured region matters. A provider that is correct in one region can be unavailable in another.

## API Keys and Server Privacy

**Use your own API credentials as the Jellyfin server administrator.** Do not put a shared project key in the source code, a README, a release archive, or a public Git repository.

The plugin asks for credentials in its Jellyfin dashboard settings page. They are saved in that Jellyfin server's plugin configuration, so `.gitignore` is not what protects them: the key never belongs in this project folder in the first place. The dashboard sends the built-in source credentials in HTTP request headers rather than putting them in source URLs.

Jellyfin's plugin configuration should still be treated as sensitive server data. Protect access to the Jellyfin dashboard, its configuration/data directory, backups, and logs. The plugin does not claim to encrypt API keys at rest.

For step-by-step setup, key rotation, and a safe custom-source example, read [API_KEYS.md](Documentation/API_KEYS.md).

<details>
<summary><strong>Get a TMDb API Read Access Token — usually only a few minutes</strong></summary>

<br />

TMDb is the easiest built-in source to begin with because it provides both regional watch-provider information and TV-network metadata. Create or sign in to a TMDb account, then open **Account settings → API** and create an application.

For a self-hosted Jellyfin server used only for your own library, choose the **personal** or **non-commercial** option if that truthfully describes your use. Do not select a personal option for a commercial project; review TMDb's terms or contact TMDb instead.

If the form asks for application information, these truthful values are appropriate for a personal server:

| Form field | Suggested value |
| --- | --- |
| Application name | `Media Tagging Manager Jellyfin Plugin` |
| Application URL | `https://github.com/mp3li/Media-Tagging-Manager-Jellyfin-Plugin` |
| Application summary / description | `A self-hosted Jellyfin plugin for my personal media library. It uses the TMDb API to identify regional watch providers and television networks for titles already in my library. Each server administrator supplies and stores their own private TMDb API Read Access Token in that server's plugin settings. The plugin does not distribute, share, or expose TMDb API credentials.` |

After TMDb approves the application, copy the **API Read Access Token**—not the older API-key value—and paste it only into **Dashboard → Plugins → Media Tagging Manager Jellyfin Plugin → Settings & sources**. Never put the token in a GitHub issue, screenshot, README, release archive, or this repository.

The plugin sends the token only in an HTTPS authorization header during TMDb requests. It does not ship a shared project key, and one person's token is never needed by another Jellyfin server.

</details>

## Requirements

To build and use the current project, you need:

- **Jellyfin 10.11.11** — the referenced Jellyfin package versions match this test server version.
- **.NET SDK 9.0** — required to build this `net9.0` plugin project.
- **Jellyfin administrator access** — required for plugin settings, scans, and manual tag edits.
- **Internet access from the Jellyfin server** — only for the sources you explicitly enable.
- **At least one enabled source** — TMDb, Watchmode, or a configured custom JSON source. A scan refuses to run without one, so it cannot accidentally clear existing plugin tags.

## Build and Install

Build a Release copy from this project folder:

```bash
dotnet publish "Media Tagging Manager/Jellyfin.Plugin.MediaTaggingManager.csproj" -c Release
```

The plugin DLL is produced here:

```text
Media Tagging Manager/bin/Release/net9.0/publish/Jellyfin.Plugin.MediaTaggingManager.dll
```

Copy the published DLL into its own folder under the Jellyfin server's `plugins` data directory, then restart Jellyfin. Keep the DLL and the installed Jellyfin server on matching package versions; Jellyfin marks incompatible plugin assemblies unsupported.

After the restart, open:

```text
Dashboard → Plugins → Media Tagging Manager Jellyfin Plugin
```

## Repository Manifest and API Keys

A Jellyfin repository manifest and API credentials are separate things.

The public manifest only tells Jellyfin which plugin release to download. It contains release metadata such as the plugin ID, version, supported Jellyfin ABI, ZIP download URL, checksum, and changelog. It must contain **no API keys**.

After a user adds the public manifest URL in Jellyfin and installs the plugin, it appears with no availability source enabled. The server administrator then opens the plugin settings, chooses their own sources, and enters their own credentials. This keeps every server's rate limits, billing, revocation, and access under that server owner's control.

The repository now has a real, checksum-backed test manifest for `0.1.0.9-test`. It points to an actual test ZIP and contains no API keys. A stable manifest entry will replace this test entry only after real Jellyfin-server testing is complete. The complete source-level compatibility review is in [the Jellyfin 10.11.11 audit](Documentation/JELLYFIN_10.11.11_COMPATIBILITY_AUDIT.md).

## Test Prerelease Catalog

`0.1.0.9-test` is a public catalog-install test build, **not** a stable release. It exists so the real Jellyfin installation flow can be tested before the first stable package is published.

To test it, add this repository URL in Jellyfin:

```text
https://raw.githubusercontent.com/mp3li/Media-Tagging-Manager-Jellyfin-Plugin/main/manifest.json
```

Then refresh the plugin catalog and install **Media Tagging Manager Jellyfin Plugin**. The test build supports Jellyfin `10.11.11`. It contains no API credentials, Jellyfin configuration, backups, logs, or media data. Record the result in [goal-testing.txt](Documentation/goal-testing.txt) before treating any feature as release-ready.

## First-Time Setup

1. Open the **Settings & sources** section.
2. Select the libraries the plugin may scan.
3. Choose **Tag providers**, **Tag networks**, or both.
4. Set the two-letter availability region, such as `US`, `GB`, `CA`, or `AU`.
5. Add a TMDb Read Access Token, a Watchmode key, a custom JSON source, or any combination of those.
6. Choose how many titles may be checked in parallel. Start conservatively if your source plan has a low rate limit.
7. Save settings.
8. Open **Scan** and run one library or all selected libraries.
9. Optionally enable **Check newly added media after Jellyfin library scans** for future incoming titles. It is off by default.

## Scanning and Progress

The Scan tab keeps the manual action close to the selected libraries:

- **Scan this library** checks one selected library.
- **Scan all selected libraries** checks every selected library.
- The status area shows the active title, completed and total counts, progress percentage, and an estimated remaining time.

Only movie and TV-series items are automatically queried in this first version. Episodes inherit their series-level availability context rather than creating a noisy duplicate availability scan for every episode.

## Library Overview and Manual Edits

The **Library overview** tab is the review-and-correction area for selected movie and TV libraries.

You can:

- show everything the plugin supports in the selected libraries
- filter by a provider
- filter by a network
- combine provider and network filters
- show titles with classifications or titles missing both kinds of tag
- edit providers and networks manually with comma-separated values

Manual edits replace only the plugin-owned tags for that title. This is useful for niche availability, regional edge cases, titles without enough external identifiers, and source corrections.

## Safety Backups and Undo

Before any tag-changing scan, incoming-media update, or manual edit, the plugin creates a complete tag backup for the affected selected libraries. A backup stores the entire tag list for every captured Jellyfin item, not just `Provider: ` and `Network: ` tags.

The **Safety backups and undo** section in settings lets an administrator:

- create a named backup at any time
- see stored backups and their item counts
- restore a specific backup
- use **Undo last tag operation** to restore the newest backup

Backups are saved in the plugin's Jellyfin data directory and remain available through server restarts. Restoring a backup overwrites the current tag list for every item in that backup, including unrelated custom tags added after the backup was made. The dashboard asks for confirmation before restoring.

## Automatic Refresh

Enable **Automatically re-check enabled sources**, then choose an interval in hours. The plugin registers a native Jellyfin scheduled task:

```text
Refresh provider and network tags
```

It also appears in **Dashboard → Scheduled Tasks**, where an administrator can inspect or run it. The configured default schedule is loaded when Jellyfin starts, so restart Jellyfin after changing the automatic-refresh setting or interval. Manual scans work immediately.

### Incoming-media checks

The **Check newly added media after Jellyfin library scans** setting is separate from the periodic full refresh.

- **On:** after Jellyfin completes a library scan, the plugin checks only new movie and series titles added since the previous enabled incoming-media check.
- **Off:** no automatic API check runs for incoming media. Manual scans and the optional scheduled full refresh still work.
- **First enable:** the plugin records a starting point rather than re-checking the entire existing library. Run a manual full scan once if you want older titles checked too.

## Custom JSON Sources

Custom sources let the plugin grow without embedding an unapproved or fragile scraper for every service. Each source defines:

- a name
- an enabled state
- a URL template
- optional `Authorization` header content
- a dot-separated JSON path for provider names
- a dot-separated JSON path for network names

Available URL tokens are:

```text
{tmdb}
{imdb}
{type}
{region}
```

Provider and network paths must resolve to either a single JSON string or an array of strings. See [API_KEYS.md](Documentation/API_KEYS.md) for a non-secret example. Only connect endpoints whose API terms allow this use.

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
├── API_KEYS.md             administrator credential and custom-source guide
├── CHANGELOG.md            unreleased and future release notes
├── goal-testing.txt        live-server testing checklist and results log
└── project-goals.txt       product goals and acceptance behavior
Tests/                      reserved for future automated tests
README.md                   project overview and runbook
```

## Known Limitations

- Current automatic querying is intentionally limited to **movies and TV series**. Music, books, episodes, and other Jellyfin item kinds are not yet source adapters in this first version.
- Availability depends on region, external IDs, API coverage, source plans, and source uptime. Missing data is not treated as proof that a title is unavailable.
- TMDb TV networks describe TV-network metadata; they are not a universal network authority for every media type.
- The dashboard's last-check details are available during the current server session; durable scan history and per-source provenance are sensible follow-up features.
- This plugin does not ship third-party API credentials and does not bypass provider logins, DRM, access restrictions, source rate limits, or terms of use.

## Responsible Use and Availability Disclaimer

This plugin organizes availability and network information for media already in a Jellyfin library. It does not provide media, unlock services, scrape account-only content, or guarantee that a provider listing is current, complete, or available to every user.

Use only your own API accounts and sources you are allowed to query. Review each source's terms, attribution requirements, rate limits, regional restrictions, and data-retention rules. Streaming catalog data changes often; treat a tag as a helpful library classification and verify important availability decisions with the provider itself.

## License

Media Tagging Manager Jellyfin Plugin by mp3li is licensed under the [GNU General Public License v3.0 or later](LICENSE) (`GPL-3.0-or-later`).
