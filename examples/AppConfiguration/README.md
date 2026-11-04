```
ooooo                                      oooo  oooo              
`888'                                      `888  `888              
 888          .ooooo.   .ooooo.   .oooo.    888   888  oooo    ooo 
 888         d88' `88b d88' `"Y8 `P  )88b   888   888   `88.  .8'
 888         888   888 888        .oP"888   888   888    `88..8'
 888       o 888   888 888   .o8 d8(  888   888   888     `888'
o888ooooood8 `Y8bod8P' `Y8bod8P' `Y888""8o o888o o888o     .8'
                                                       .o..P'
                                                       `Y8P'
```

# Example: Use Azure App Configuration from an Aspire app within Locally

This example shows how to use Azure App Configuration from an [Aspire](https://aspire.dev) app on [Locally Build](https://locally.build).

When the AppHost is run using `locally run aspire run`, Aspire provisions the App Configuration Store in Locally (rather than in Azure) and grants you access to it, just as it would in an Azure Subscription. The Api then loads its configuration from the App Configuration Store (refreshing it as settings change), signing in using the identity which `locally run` provides - as such there's no need for any Access Keys, or Connection Strings containing secrets.

```
 curl ─▶ api ──┬─ PUT /settings/{key} ──▶ App Configuration (ConfigurationClient)
               └─ GET /settings/{key} ◀── IConfiguration ◀── App Configuration (refreshed every second)
```

This example is made up of three projects, each written in C# using .NET 10:

* [`Locally.Examples.Aspire.AppConfiguration.AppHost`](./Locally.Examples.Aspire.AppConfiguration.AppHost/Program.cs) - the AppHost, which defines an App Configuration Store and references it from the Api.
* [`Locally.Examples.Aspire.AppConfiguration.Api`](./Locally.Examples.Aspire.AppConfiguration.Api/Program.cs) - the Api, which loads its configuration from the App Configuration Store and refreshes it every second. `PUT /settings/{key}` stores a setting using a `ConfigurationClient`, whilst `GET /settings/{key}` reads it from the app's `IConfiguration`.
* [`Locally.Examples.Aspire.AppConfiguration.Tests`](./Locally.Examples.Aspire.AppConfiguration.Tests/) - the tests, which start the AppHost using `Aspire.Hosting.Testing`, store a setting using the Api and then check that it shows up in the app's configuration once it's refreshed (this is what runs in GitHub Actions).

## The Aspire packages

| Package | Used in | What it does here |
| --- | --- | --- |
| [`Aspire.Hosting.Azure.AppConfiguration`](https://www.nuget.org/packages/Aspire.Hosting.Azure.AppConfiguration) | AppHost | Defines the App Configuration Store. |
| [`Locally.Aspire.Hosting`](../../src/Locally.Aspire.Hosting) | AppHost | `builder.AddLocally()` - which means that when run using `locally run`, `aspire run` provisions the App Configuration Store in Locally rather than in Azure. |
| [`Aspire.Microsoft.Extensions.Configuration.AzureAppConfiguration`](https://www.nuget.org/packages/Aspire.Microsoft.Extensions.Configuration.AzureAppConfiguration) | Api | Loads the Api's configuration from the App Configuration Store, using the endpoint which the AppHost passes to the Api. |
| [`Microsoft.Azure.AppConfiguration.AspNetCore`](https://www.nuget.org/packages/Microsoft.Azure.AppConfiguration.AspNetCore), [`Azure.Data.AppConfiguration`](https://www.nuget.org/packages/Azure.Data.AppConfiguration) | Api | `UseAzureAppConfiguration()`, which refreshes the configuration as requests come in - and the `ConfigurationClient` used to store settings. |
| [`Locally.Aspire.Client`](../../src/Locally.Aspire.Client) | Api | `new LocallyCredential()` - which the configuration provider and the `ConfigurationClient` are given to sign in to Locally (see below). |

Since this example lives within this repository, the AppHost and the Api reference `Locally.Aspire.Hosting` and `Locally.Aspire.Client` from [`src/`](../../src) (which means they're always tested against the latest code) - however in your own app you'll want to add the packages instead, like so:

```bash
dotnet add package Locally.Aspire.Hosting # in the AppHost
dotnet add package Locally.Aspire.Client  # in the Api
```

## Requirements

* [Locally Build](https://locally.build).
* The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
* The [Aspire CLI](https://aspire.dev).
* Either [Docker](https://www.docker.com) or [Podman](https://podman.io) (recommended).
* The Locally Plugin for `Microsoft.AppConfiguration` installed (`locally plugin install --name Microsoft.AppConfiguration`).

## Running the example

First, make sure your container runtime (Docker or Podman) is running, then start Locally:

```bash
locally build
```

Once Locally is running, open another terminal and run the AppHost:

```bash
cd examples/AppConfiguration
locally run aspire run --apphost Locally.Examples.Aspire.AppConfiguration.AppHost
```

The first time this is run, Aspire creates a Resource Group named `rg-aspire-locallyexamplesaspireappconfigurationapphost-<hash>`, provisions the resources within it and then grants your `locally run` identity (which the Api signs in as) access to them. Aspire then stores the details of what it's provisioned in the AppHost's User Secrets, and reuses these on subsequent runs.

Once it's running, Aspire outputs a link to the Aspire Dashboard - and the Api is available at `http://localhost:5087`.

To store a setting:

```bash
curl -i -X PUT -d 'hello from Locally' http://localhost:5087/settings/Greeting
```

```
HTTP/1.1 204 No Content
```

Which then shows up in the app's configuration once it's been refreshed - since this happens in the background as requests come in, this returns a `404 Not Found` for the first couple of seconds (and the first few requests):

```bash
curl http://localhost:5087/settings/Greeting
```

```json
"hello from Locally"
```

### Running the tests

Since the tests start the AppHost themselves, there's no need to run `aspire run` first - as such these can be run using:

```bash
cd examples/AppConfiguration
locally run dotnet test Locally.Examples.Aspire.AppConfiguration.slnx
```

## Notes

Since Locally starts empty each time it's launched, after a restart the resources Aspire has stored in the AppHost's User Secrets will no longer exist. `AddLocally()` detects this and Aspire provisions them again - subsequent runs then reuse these, which means they start in a few seconds.

Aspire's App Configuration provider creates a credential of its own rather than using the one which `UseLocallyCredential()` configures - as such the Api gives it a `LocallyCredential`, like so:

```csharp
builder.AddAzureAppConfiguration("config", settings => settings.Credential = new LocallyCredential());
```

Without this, when `Optional = true` is set the provider fails silently - and the app starts with no settings from App Configuration at all. The `ConfigurationClient` is created by the Api itself, and as such is given a `LocallyCredential` too.

The AppHost intentionally doesn't call `RunAsEmulator()`, since the App Configuration Store is provided by Locally - as such when run without `locally run`, `aspire run` provisions the App Configuration Store in Azure, as it would for any other Aspire app.

To keep this example focused on Azure App Configuration, the Api doesn't require authentication - however a real app would want to require this.

## Tearing it down

First stop the AppHost using `Ctrl+C`, then delete the Resource Group which Aspire created:

```bash
locally run az group delete --yes --name "$(locally run az group list \
  --query "[?starts_with(name, 'rg-aspire-locallyexamplesaspireappconfigurationapphost-')].name | [0]" --output tsv)"
```
