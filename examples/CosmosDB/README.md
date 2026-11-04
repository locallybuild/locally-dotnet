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

# Example: Use Azure Cosmos DB from an Aspire app within Locally

This example shows how to use Azure Cosmos DB from an [Aspire](https://aspire.dev) app on [Locally Build](https://locally.build).

When the AppHost is run using `locally run aspire run`, Aspire provisions the Cosmos DB Account, its Database and its Container in Locally (rather than in Azure) and grants you access to them, just as it would in an Azure Subscription. The Api then signs in to the Cosmos DB Account using the identity which `locally run` provides - as such there's no need for any Access Keys, or Connection Strings containing secrets.

```
 curl ─▶ api ──┬─ PUT /items/{id} ──▶ Cosmos DB (app database, items container)
               └─ GET /items/{id} ◀── Cosmos DB (app database, items container)
```

This example is made up of three projects, each written in C# using .NET 10:

* [`Locally.Examples.Aspire.CosmosDB.AppHost`](./Locally.Examples.Aspire.CosmosDB.AppHost/Program.cs) - the AppHost, which defines a Cosmos DB Account containing the `app` Database and the `items` Container - and references it from the Api.
* [`Locally.Examples.Aspire.CosmosDB.Api`](./Locally.Examples.Aspire.CosmosDB.Api/Program.cs) - the Api, which stores items in the Container using `PUT /items/{id}`, and reads them back using `GET /items/{id}`.
* [`Locally.Examples.Aspire.CosmosDB.Tests`](./Locally.Examples.Aspire.CosmosDB.Tests/) - the tests, which start the AppHost using `Aspire.Hosting.Testing`, store an item using the Api and then check that it's read back from the Container (this is what runs in GitHub Actions).

## The Aspire packages

| Package | Used in | What it does here |
| --- | --- | --- |
| [`Aspire.Hosting.Azure.CosmosDB`](https://www.nuget.org/packages/Aspire.Hosting.Azure.CosmosDB) | AppHost | Defines the Cosmos DB Account, along with the `app` Database and the `items` Container. |
| [`Locally.Aspire.Hosting`](../../src/Locally.Aspire.Hosting) | AppHost | `builder.AddLocally()` - which means that when run using `locally run`, `aspire run` provisions the Cosmos DB Account in Locally rather than in Azure. |
| [`Aspire.Microsoft.Azure.Cosmos`](https://www.nuget.org/packages/Aspire.Microsoft.Azure.Cosmos) | Api | Configures the Cosmos DB client using the Connection String which the AppHost passes to the Api. |
| [`Locally.Aspire.Client`](../../src/Locally.Aspire.Client) | Api | `new LocallyCredential()` - which the Cosmos DB client is given to sign in to Locally (see below). |

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
* The Locally Plugin for `Microsoft.DocumentDB` installed (`locally plugin install --name Microsoft.DocumentDB`).

## Running the example

First, make sure your container runtime (Docker or Podman) is running, then start Locally:

```bash
locally build
```

Once Locally is running, open another terminal and run the AppHost:

```bash
cd examples/CosmosDB
locally run aspire run --apphost Locally.Examples.Aspire.CosmosDB.AppHost
```

The first time this is run, Aspire creates a Resource Group named `rg-aspire-locallyexamplesaspirecosmosdbapphost-<hash>`, provisions the resources within it and then grants your `locally run` identity (which the Api signs in as) access to them. Aspire then stores the details of what it's provisioned in the AppHost's User Secrets, and reuses these on subsequent runs.

Once it's running, Aspire outputs a link to the Aspire Dashboard - and the Api is available at `http://localhost:5086`.

To store an item:

```bash
curl -i -X PUT -d 'hello from Locally' http://localhost:5086/items/first
```

```
HTTP/1.1 204 No Content
```

Which can then be read back from the Container:

```bash
curl http://localhost:5086/items/first
```

```json
{"id":"first","text":"hello from Locally"}
```

### Running the tests

Since the tests start the AppHost themselves, there's no need to run `aspire run` first - as such these can be run using:

```bash
cd examples/CosmosDB
locally run dotnet test Locally.Examples.Aspire.CosmosDB.slnx
```

## Notes

Since Locally starts empty each time it's launched, after a restart the resources Aspire has stored in the AppHost's User Secrets will no longer exist. `AddLocally()` detects this and Aspire provisions them again - subsequent runs then reuse these, which means they start in a few seconds.

Aspire's Cosmos DB client creates a credential of its own rather than using the one which `UseLocallyCredential()` configures - as such the Api gives it a `LocallyCredential`, like so:

```csharp
builder.AddAzureCosmosClient("cosmos", settings => settings.Credential = new LocallyCredential());
```

The AppHost intentionally doesn't call `RunAsEmulator()`, since the Cosmos DB Account is provided by Locally - as such when run without `locally run`, `aspire run` provisions the Cosmos DB Account in Azure, as it would for any other Aspire app.

To keep this example focused on Azure Cosmos DB, the Api doesn't require authentication - however a real app would want to require this.

## Tearing it down

First stop the AppHost using `Ctrl+C`, then delete the Resource Group which Aspire created:

```bash
locally run az group delete --yes --name "$(locally run az group list \
  --query "[?starts_with(name, 'rg-aspire-locallyexamplesaspirecosmosdbapphost-')].name | [0]" --output tsv)"
```
