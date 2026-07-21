# API Keys and Source Setup

This guide is for the Jellyfin server administrator. It contains no real credentials and should remain safe to publish.

## The basic rule

Each Jellyfin server uses its **own** source credentials. Do not add a key to `PluginConfiguration.cs`, `configPage.html`, a release archive, a screenshot, a bug report, or a Git commit.

Enter credentials only in:

```text
Jellyfin Dashboard → Plugins → Media Tagging Manager Jellyfin Plugin → Settings & sources
```

The plugin stores those values with the Jellyfin server's plugin configuration. That configuration is not part of this repository and is not protected by this repository's `.gitignore`. Limit access to the Jellyfin host, dashboard, configuration directory, backups, and logs.

## TMDb

TMDb is the recommended first source because it supplies both regional watch-provider data and television-network information.

1. Create or sign in to a TMDb developer account.
2. Create an API application and copy its **API Read Access Token**.
3. Paste the token into the plugin's **TMDb API Read Access Token** field.
4. Set the availability region and save.
5. Run a scan of a small test library before scanning everything.

The plugin sends the token as a Bearer authorization header. It does not place the token in a request URL.

## Watchmode

Watchmode is optional additional provider coverage.

1. Create a Watchmode API account and obtain a key for the plan you intend to use.
2. Paste it into **Watchmode API key**.
3. Confirm that the library items have IMDb IDs; Watchmode lookups need them in the current adapter.
4. Start with a small library and choose a low parallel-title count if the plan has strict request limits.

The plugin sends this key in the `X-API-Key` HTTP header. It does not place it in a request URL.

## Key rotation or removal

To rotate a credential, replace its value in the plugin settings and save. To stop using a source, clear its key or disable its custom-source entry, save, and restart Jellyfin if you changed the automatic schedule.

Removing a key does not remove existing provider/network tags. That is deliberate: a source being disabled is not evidence that its prior result was wrong.

## Custom JSON source example

The additional-source field accepts an array like this. The endpoint below is only a shape example; it is not a real service or a credential.

```json
[
  {
    "Name": "Example licensed catalog",
    "Enabled": true,
    "UrlTemplate": "https://catalog.example.invalid/title?tmdb={tmdb}&imdb={imdb}&region={region}",
    "Authorization": "Bearer paste-your-source-token-here",
    "ProviderPath": "availability.providers",
    "NetworkPath": "metadata.networks"
  }
]
```

For that example, the source response would need this general shape:

```json
{
  "availability": {
    "providers": ["Example Stream", "Example Free"]
  },
  "metadata": {
    "networks": ["Example Network"]
  }
}
```

Before enabling a custom source, check that its terms allow your intended use, its authentication method fits the single `Authorization` header model, and its rate limit can handle the selected libraries and schedule.

## Future source decisions

Do not add a source merely because it has visible data on a website. Prefer a documented, authorized API that supports your region and identifiers. Good candidates for a future first-class adapter include Streaming Availability API for broader availability coverage and a dedicated network-metadata source such as TVMaze or TheTVDB where its terms and matching quality are suitable.
