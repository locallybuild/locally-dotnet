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

# Locally for .NET

This repository contains the .NET packages for using [Aspire](https://aspire.dev) with [Locally Build](https://locally.build), which runs Azure on your machine.

With them, `aspire run` and `aspire deploy` provision your AppHost's Azure Resources in Locally instead of Azure, and your services sign in to them as the identity `locally run` provides - and since these don't change anything outside of Locally, the same code still publishes and runs against Azure.

| Package | Use it in | What it does |
| --- | --- | --- |
| [`Locally.Aspire.Hosting`](./src/Locally.Aspire.Hosting) | your Aspire AppHost | `builder.AddLocally()`. Under `locally run`, `aspire run` and `aspire deploy` provision the AppHost's Azure Resources in Locally instead of Azure. |
| [`Locally.Aspire.Client`](./src/Locally.Aspire.Client) | your services | `builder.UseLocallyCredential()`. Signs the service's Azure clients in to Locally, both under `locally run` and once it's deployed into Locally. |

## Requirements

* [Locally Build](https://locally.build).
* The [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer.
* The [Aspire CLI](https://aspire.dev) (these packages are built against Aspire 13.6.4).
* Either [Docker](https://www.docker.com) or [Podman](https://podman.io) (recommended).
* The Locally Plugin for each Azure service your AppHost uses, e.g. `Microsoft.Storage` (`locally plugin install --name Microsoft.Storage`).

The [`Locally.Aspire.Hosting` README](./src/Locally.Aspire.Hosting#supported-integrations) lists which Aspire integrations work with Locally today.

## Getting started

First up, add `Locally.Aspire.Hosting` to your AppHost project:

```bash
dotnet add package Locally.Aspire.Hosting
```

And then call `AddLocally()` when building the AppHost:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
builder.AddLocally();

var storage = builder.AddAzureStorage("storage");
// ...
```

> [!NOTE]
> Since Aspire doesn't provision resources configured via `RunAsEmulator()` or `RunAsContainer()`, you'll need to remove those calls for any resources which should be provisioned in Locally.

Next, add `Locally.Aspire.Client` to each of your Service Projects:

```bash
dotnet add package Locally.Aspire.Client
```

And then call `UseLocallyCredential()` before registering your Azure clients:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.UseLocallyCredential();

builder.AddAzureBlobContainerClient("entries");
```

Next we need Locally to be running, which can be launched like so:

```bash
locally build
```

Then, in another terminal, run the AppHost within Locally:

```bash
locally run dotnet aspire run
```

Aspire provisions the AppHost's Azure Resources in Locally, grants you access to them and hands your services their Connection Strings, just as it would in an Azure Subscription. Clicking a resource's link in the Aspire Dashboard opens it in [the Locally Dashboard](https://localhost:5678) rather than the Azure Portal.

## Examples

There's a complete example for each of the Aspire integrations that work with Locally today within [`examples/`](./examples) - each made up of an AppHost, an Api which uses the resource and a test which runs against Locally (which also runs in GitHub Actions):

| Integration | Example | Notes |
| --- | --- | --- |
| Storage (blobs, queues and tables) | [`examples/Storage`](./examples/Storage) | |
| Azure Managed Redis and Azure Cache for Redis | [`examples/Redis`](./examples/Redis) | You'll want to keep `WithAzureAuthentication()`, which signs in to caches in Azure. |
| Service Bus (queues, topics and subscriptions) | [`examples/ServiceBus`](./examples/ServiceBus) | |
| Key Vault | [`examples/KeyVault`](./examples/KeyVault) | |
| PostgreSQL Flexible Server | [`examples/PostgreSQL`](./examples/PostgreSQL) | The data source needs to be given a `LocallyCredential`. |
| Application Insights and Log Analytics | [`examples/ApplicationInsights`](./examples/ApplicationInsights) | |
| Cosmos DB | [`examples/CosmosDB`](./examples/CosmosDB) | The client needs to be given a `LocallyCredential`. |
| App Configuration | [`examples/AppConfiguration`](./examples/AppConfiguration) | The provider needs to be given a `LocallyCredential`. |
| Azure SQL | [`examples/AzureSQL`](./examples/AzureSQL) | SqlClient needs to be given an access token from a `LocallyCredential`. |
| Event Hubs | [`examples/EventHubs`](./examples/EventHubs) | |

For the most part this is the same code you'd write for Azure - the exceptions being PostgreSQL, Cosmos DB, App Configuration and Azure SQL, whose clients create a credential of their own (rather than using the one `UseLocallyCredential()` configures), as such each of these examples explains what's needed.

For everything else, including the integrations that don't work in Locally yet, see the [`Locally.Aspire.Hosting` README](./src/Locally.Aspire.Hosting#supported-integrations).

Separately, [`locallybuild/example-app-service-aspire`](https://github.com/locallybuild/example-app-service-aspire) shows a larger app using both packages - which runs using `aspire run`, and deploys to App Service using `aspire publish`.

## Documentation

Each package has its own README, which covers what it changes and its known limitations:

* [`Locally.Aspire.Hosting`](./src/Locally.Aspire.Hosting/README.md): what `AddLocally()` changes, emulators and containers, and supported integrations.
* [`Locally.Aspire.Client`](./src/Locally.Aspire.Client/README.md): what `UseLocallyCredential()` changes, using a real Azure resource alongside Locally, and clients it can't reach.

## Developing

Building and testing these packages requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). With that installed, the tests can be run via:

```bash
dotnet test Locally.Aspire.slnx
```

And the packages can be built into `./artifacts` via:

```bash
dotnet pack Locally.Aspire.slnx --output artifacts
```

## License

[Apache 2.0](./LICENSE).
