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

# Example: Use Azure Managed Redis from an Aspire app within Locally

This example shows how to use Azure Managed Redis from an [Aspire](https://aspire.dev) app on [Locally Build](https://locally.build).

When the AppHost is run using `locally run aspire run`, Aspire provisions the cache in Locally (rather than in Azure) and grants you access to it, just as it would in an Azure Subscription. The Api then signs in to the cache using the identity which `locally run` provides - as such there's no need for any Access Keys, or Connection Strings containing secrets.

```
 curl ─▶ api ──┬─ PUT /values/{key} ──▶ Azure Managed Redis
               └─ GET /values/{key} ◀── Azure Managed Redis
```

This example is made up of three projects, each written in C# using .NET 10:

* [`Locally.Examples.Aspire.Redis.AppHost`](./Locally.Examples.Aspire.Redis.AppHost/Program.cs) - the AppHost, which defines an Azure Managed Redis cache and references it from the Api.
* [`Locally.Examples.Aspire.Redis.Api`](./Locally.Examples.Aspire.Redis.Api/Program.cs) - the Api, which stores values in the cache using `PUT /values/{key}`, and reads them back using `GET /values/{key}`.
* [`Locally.Examples.Aspire.Redis.Tests`](./Locally.Examples.Aspire.Redis.Tests/) - the tests, which start the AppHost using `Aspire.Hosting.Testing`, store a value using the Api and then check that it's read back from the cache (this is what runs in GitHub Actions).

## The Aspire packages

| Package | Used in | What it does here |
| --- | --- | --- |
| [`Aspire.Hosting.Azure.Redis`](https://www.nuget.org/packages/Aspire.Hosting.Azure.Redis) | AppHost | Defines the Azure Managed Redis cache. |
| [`Locally.Aspire.Hosting`](../../src/Locally.Aspire.Hosting) | AppHost | `builder.AddLocally()` - which means that when run using `locally run`, `aspire run` provisions the cache in Locally rather than in Azure. |
| [`Aspire.StackExchange.Redis`](https://www.nuget.org/packages/Aspire.StackExchange.Redis), [`Aspire.Microsoft.Azure.StackExchangeRedis`](https://www.nuget.org/packages/Aspire.Microsoft.Azure.StackExchangeRedis) | Api | Configures the Redis client using the Connection String which the AppHost passes to the Api, and `WithAzureAuthentication()` signs in to caches in Azure. |
| [`Locally.Aspire.Client`](../../src/Locally.Aspire.Client) | Api | `builder.UseLocallyCredential()` - which signs the Api's Redis client in to caches in Locally. |

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
* The Locally Plugin for `Microsoft.Cache` installed (`locally plugin install --name Microsoft.Cache`).

## Running the example

First, make sure your container runtime (Docker or Podman) is running, then start Locally:

```bash
locally build
```

Once Locally is running, open another terminal and run the AppHost:

```bash
cd examples/Redis
locally run aspire run --apphost Locally.Examples.Aspire.Redis.AppHost
```

The first time this is run, Aspire creates a Resource Group named `rg-aspire-locallyexamplesaspireredisapphost-<hash>`, provisions the resources within it and then grants your `locally run` identity (which the Api signs in as) access to them. Aspire then stores the details of what it's provisioned in the AppHost's User Secrets, and reuses these on subsequent runs.

Once it's running, Aspire outputs a link to the Aspire Dashboard - and the Api is available at `http://localhost:5081`.

To store a value:

```bash
curl -i -X PUT -d 'hello from Locally' http://localhost:5081/values/greeting
```

```
HTTP/1.1 204 No Content
```

Which can then be read back from the cache:

```bash
curl http://localhost:5081/values/greeting
```

```json
"hello from Locally"
```

### Running the tests

Since the tests start the AppHost themselves, there's no need to run `aspire run` first - as such these can be run using:

```bash
cd examples/Redis
locally run dotnet test Locally.Examples.Aspire.Redis.slnx
```

## Notes

Since Locally starts empty each time it's launched, after a restart the resources Aspire has stored in the AppHost's User Secrets will no longer exist. `AddLocally()` detects this and Aspire provisions them again - subsequent runs then reuse these, which means they start in a few seconds.

You'll want to keep `WithAzureAuthentication()` in place, since that's what signs in to caches in Azure - however since this only signs in to caches using Azure's host names (e.g. `*.redis.azure.net`), `UseLocallyCredential()` signs in to those in Locally.

The same Api works against Azure Cache for Redis too - to use that instead, swap `AddAzureManagedRedis("cache")` for `AddAzureRedis("cache")` in the AppHost.

To keep this example focused on Azure Managed Redis, the Api doesn't require authentication - however a real app would want to require this.

## Tearing it down

First stop the AppHost using `Ctrl+C`, then delete the Resource Group which Aspire created:

```bash
locally run az group delete --yes --name "$(locally run az group list \
  --query "[?starts_with(name, 'rg-aspire-locallyexamplesaspireredisapphost-')].name | [0]" --output tsv)"
```
