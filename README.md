# OAuth Handler
This project is an OAuth handler written for Duplicati, but is flexible enough to be used for other projects.

In the current form, the OAuth handler can be used to set up a self-hosted OAuth infrastructure, instead of relying on the Duplicati Community service.

# Usage

Look in the [./config.json](./config.json) file to see the setup. The default `config.json` file can be used as-is for most uses, or a custom file can be used to add/tweak the configuration. The `config.json` file is embedded in the application and loaded, if no config file is supplied.

## Environment variables

The application is designed to be runnable as a Docker application, so everything is configurable via environment variables. At least two variables must be set:

- `HOSTNAME`: the hostname used in callbacks. Examples: `localhost:12345`, `oauth.example.com`.
- `APPNAME`: the app name shown to the user. Examples: `Duplicati`, `Cool app name`

Optional environment variables:

- `DISPLAYNAME`: the value displayed to end users on html rendered pages
- `SERVICES`: a comma separated list of services to enable; if this value is empty, all services are enabled. Enabling a serviceId (e.g., `gd` enables all services that use this). Example: `gd,gcs,dropbox`
- `SECRETS`: path to an existing JSON file with key/value pairs that are injected as secrets
- `SECRETS_PASSPHRASE`: path to an existing JSON file with key/value pairs that are injected as secrets. Use [AESCrypt](https://www.aescrypt.com) to encrypt the file
- `CONFIGFILE`: path to an existing JSON file with a custom config. If this option is set, services with the same id will override the embedded `config.json` services with the same id.
- `STORAGE`: a connection string for indicating where the V1 tokens are stored. If not set, all connections will use V2.
- `SEQ_URL`: url for logging to Seq
- `SEQ_APIKEY`: API key for logging to Seq

## ASP.Net variables

Since the application is based on ASP.Net, the variables for ASP.Net are also relevant. Most importantly:

- `ASPNETCORE_HTTP_PORTS`: the port(s) the application is listening on

## Secrets

Before the service can be used, there needs to be a number of secrets set up. These secrets can either be injected via a `secrets.json` file or from environment variables (if both are set, the environment variable takes precedence).

Currently it is possible to provide these secrets:
- `GCS_CLIENT_ID` + `GCS_CLIENT_SECRET`: Google Cloud Services OAuth values
- `WL_CLIENT_ID` + `WL_CLIENT_SECRET`: Windows Live OAuth values
- `MSGRAPH_CLIENT_ID` + `MSGRAPH_CLIENT_SECRET`: Microsoft Graph API OAuth values
- `BOX_CLIENT_ID` + `BOX_CLIENT_SECRET`: Box.com OAuth values
- `DROPBOX_CLIENT_ID` + `DROPBOX_CLIENT_SECRET`: Dropbox OAuth values
- `JOTTA_CLIENT_SECRET`: Jottacloud API key

An example `secrets.json` file would look like:
```
{
    "GD_CLIENT_ID": "google-client-id",
    "GD_CLIENT_SECRET": "google-client-secret"
}
```

## Base64 encoding
The options `SECRETS` and `CONFIGFILE` can be encoded with base64 encoding and provided via the environment variables, which enables deployment in scenarios where there is no persistent storage available.

Use the prefix `base64:` to encode it, for instance the value `{'id': 1}` should be provided as `base64:eydpZCc6IDF9`.
For the `SECRETS` variable this can be done with or without encryption applied via `SECRETS_PASSPHRASE`.

## V1 vs V2 token format

The original V1 token is essentially a random id, followed by a passphrase. The id is used to locate the encrypted blob with the refresh token, and the password is used to decrypt the blob. Using the V1 setup allows the OAuth provider to update the refresh token, without changing the client details. The V2 token is indicated by starting with V2, then the service name, and then the refresh token.

The V2 format is more efficient for the server and does not require any storage. However, most OAuth providers will rotate the refresh token, and since the key is encoded into the token, the user would need to somehow update the token. For this reason, the backends will all use V1 if storage is enabled.