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

# Example: Use Azure Storage from an Aspire app within Locally

This example shows how to use Azure Storage (blobs, queues and tables) from an [Aspire](https://aspire.dev) app on [Locally Build](https://locally.build).

When the AppHost is run using `locally run aspire run`, Aspire provisions the Storage Account in Locally (rather than in Azure) and grants you access to it, just as it would in an Azure Subscription. The Api then signs in to the Storage Account using the identity which `locally run` provides - as such there's no need for any Access Keys, or Connection Strings containing secrets.

```
 curl ─▶ api ──┬─ POST /entries/{id} ──▶ Blob Storage  (entries/{id})
               │                     ├─▶ Table Storage (entries, one row per entry)
               │                     └─▶ Queue Storage (submissions)
               ├─ GET  /entries/{id} ◀── Blob Storage + Table Storage
               └─ POST /submissions/next ◀── Queue Storage
```

This example is made up of three projects, each written in C# using .NET 10:

* [`Locally.Examples.Aspire.Storage.AppHost`](./Locally.Examples.Aspire.Storage.AppHost/Program.cs) - the AppHost, which defines a Storage Account containing the `entries` Blob Container, the `submissions` Queue and Table Storage - and references each of these from the Api.
* [`Locally.Examples.Aspire.Storage.Api`](./Locally.Examples.Aspire.Storage.Api/Program.cs) - the Api, which stores each entry as both a Blob and a row in a Table, and then adds its ID to the Queue. `GET /entries/{id}` reads the entry back from both the Blob and the Table, whilst `POST /submissions/next` takes the next ID off the Queue.
* [`Locally.Examples.Aspire.Storage.Tests`](./Locally.Examples.Aspire.Storage.Tests/StorageTests.cs) - the tests, which start the AppHost using `Aspire.Hosting.Testing`, store an entry using the Api and then check that it's returned from each of the above (this is what runs in GitHub Actions).

## The Aspire packages

| Package | Used in | What it does here |
| --- | --- | --- |
| [`Aspire.Hosting.Azure.Storage`](https://www.nuget.org/packages/Aspire.Hosting.Azure.Storage) | AppHost | Defines the Storage Account, along with the `entries` Blob Container, the `submissions` Queue and Table Storage. |
| [`Locally.Aspire.Hosting`](../../src/Locally.Aspire.Hosting) | AppHost | `builder.AddLocally()` - which means that when run using `locally run`, `aspire run` provisions the Storage Account in Locally rather than in Azure. |
| [`Aspire.Azure.Storage.Blobs`](https://www.nuget.org/packages/Aspire.Azure.Storage.Blobs), [`Aspire.Azure.Storage.Queues`](https://www.nuget.org/packages/Aspire.Azure.Storage.Queues), [`Aspire.Azure.Data.Tables`](https://www.nuget.org/packages/Aspire.Azure.Data.Tables) | Api | Configures the Blob Container, Queue and Table clients using the Connection Strings which the AppHost passes to the Api. |
| [`Locally.Aspire.Client`](../../src/Locally.Aspire.Client) | Api | `builder.UseLocallyCredential()` - which signs the Api's Azure clients in to Locally. |

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
* The Locally Plugin for `Microsoft.Storage` installed (`locally plugin install --name Microsoft.Storage`).

## Running the example

First, make sure your container runtime (Docker or Podman) is running, then start Locally:

```bash
locally build
```

Once Locally is running, open another terminal and run the AppHost:

```bash
cd examples/Storage
locally run aspire run --apphost Locally.Examples.Aspire.Storage.AppHost
```

The first time this is run, Aspire creates a Resource Group named `rg-aspire-locallyexamplesaspirestorageapphost-<hash>`, provisions the Storage Account within it and then grants your `locally run` identity (which the Api signs in as) access to it. Aspire then stores the details of what it's provisioned in the AppHost's User Secrets, and reuses these on subsequent runs.

Once it's running, Aspire outputs a link to the Aspire Dashboard - and the Api is available at `http://localhost:5080`.

To store an entry:

```bash
curl -i -X POST -d 'hello from Locally' http://localhost:5080/entries/first
```

```
HTTP/1.1 201 Created
Location: /entries/first
```

Which can then be read back from both the Blob and the Table:

```bash
curl http://localhost:5080/entries/first
```

```json
{"id":"first","text":"hello from Locally","length":18}
```

And finally, the ID of the entry can be taken off the Queue - once the Queue is empty this returns a `204 No Content`:

```bash
curl -X POST http://localhost:5080/submissions/next
```

```json
"first"
```

Clicking on the Storage Account within the Aspire Dashboard opens it within [the Locally Dashboard](https://localhost:5678) (rather than the Azure Portal).

### Running the tests

Since the tests start the AppHost themselves, there's no need to run `aspire run` first - as such these can be run using:

```bash
cd examples/Storage
locally run dotnet test Locally.Examples.Aspire.Storage.slnx
```

## Notes

Since Locally starts empty each time it's launched, after a restart the resources Aspire has stored in the AppHost's User Secrets will no longer exist. `AddLocally()` detects this and Aspire provisions them again - subsequent runs then reuse these, which means they start in a few seconds.

The AppHost intentionally doesn't call `RunAsEmulator()`, since the Storage Account is provided by Locally - as such when run without `locally run`, `aspire run` provisions the Storage Account in Azure, as it would for any other Aspire app.

To keep this example focused on Azure Storage, the Api doesn't require authentication - however a real app would want to require this.

## Tearing it down

First stop the AppHost using `Ctrl+C`, then delete the Resource Group which Aspire created:

```bash
locally run az group delete --yes --name "$(locally run az group list \
  --query "[?starts_with(name, 'rg-aspire-locallyexamplesaspirestorageapphost-')].name | [0]" --output tsv)"
```
