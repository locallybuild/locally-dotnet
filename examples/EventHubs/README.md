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

# Example: Use Azure Event Hubs from an Aspire app within Locally

This example shows how to send and receive events using Azure Event Hubs from an [Aspire](https://aspire.dev) app on [Locally Build](https://locally.build).

When the AppHost is run using `locally run aspire run`, Aspire provisions the Event Hubs Namespace and its Event Hub in Locally (rather than in Azure) and grants you access to them, just as it would in an Azure Subscription. The Api then signs in to the Event Hub using the identity which `locally run` provides - as such there's no need for any Access Keys, or Connection Strings containing secrets.

```
 curl ─▶ api ──┬─ POST /events ──▶ Event Hub (telemetry)
               └─ GET  /events ◀── Event Hub (telemetry, from the earliest event)
```

This example is made up of three projects, each written in C# using .NET 10:

* [`Locally.Examples.Aspire.EventHubs.AppHost`](./Locally.Examples.Aspire.EventHubs.AppHost/Program.cs) - the AppHost, which defines an Event Hubs Namespace containing the `telemetry` Event Hub - and references it from the Api.
* [`Locally.Examples.Aspire.EventHubs.Api`](./Locally.Examples.Aspire.EventHubs.Api/Program.cs) - the Api, which sends events to the Event Hub using `POST /events`, and reads every event in the Event Hub (from the earliest) using `GET /events`.
* [`Locally.Examples.Aspire.EventHubs.Tests`](./Locally.Examples.Aspire.EventHubs.Tests/) - the tests, which start the AppHost using `Aspire.Hosting.Testing`, send an event using the Api and then check that it's received from the Event Hub (this is what runs in GitHub Actions).

## The Aspire packages

| Package | Used in | What it does here |
| --- | --- | --- |
| [`Aspire.Hosting.Azure.EventHubs`](https://www.nuget.org/packages/Aspire.Hosting.Azure.EventHubs) | AppHost | Defines the Event Hubs Namespace and the `telemetry` Event Hub. |
| [`Locally.Aspire.Hosting`](../../src/Locally.Aspire.Hosting) | AppHost | `builder.AddLocally()` - which means that when run using `locally run`, `aspire run` provisions the Event Hubs Namespace in Locally rather than in Azure. |
| [`Aspire.Azure.Messaging.EventHubs`](https://www.nuget.org/packages/Aspire.Azure.Messaging.EventHubs) | Api | Configures the Event Hub producer and consumer clients using the Connection String which the AppHost passes to the Api. |
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
* The Locally Plugin for `Microsoft.EventHub` installed (`locally plugin install --name Microsoft.EventHub`).

## Running the example

First, make sure your container runtime (Docker or Podman) is running, then start Locally:

```bash
locally build
```

Once Locally is running, open another terminal and run the AppHost:

```bash
cd examples/EventHubs
locally run aspire run --apphost Locally.Examples.Aspire.EventHubs.AppHost
```

The first time this is run, Aspire creates a Resource Group named `rg-aspire-locallyexamplesaspireeventhubsapphost-<hash>`, provisions the resources within it and then grants your `locally run` identity (which the Api signs in as) access to them. Aspire then stores the details of what it's provisioned in the AppHost's User Secrets, and reuses these on subsequent runs.

Once it's running, Aspire outputs a link to the Aspire Dashboard - and the Api is available at `http://localhost:5089`.

To send an event:

```bash
curl -i -X POST -d 'hello from Locally' http://localhost:5089/events
```

```
HTTP/1.1 202 Accepted
```

Which can then be read back from the Event Hub - since this reads every event in the Event Hub (from the earliest), this'll also include any events sent previously (e.g. by the tests):

```bash
curl http://localhost:5089/events
```

```json
["hello from Locally"]
```

### Running the tests

Since the tests start the AppHost themselves, there's no need to run `aspire run` first - as such these can be run using:

```bash
cd examples/EventHubs
locally run dotnet test Locally.Examples.Aspire.EventHubs.slnx
```

## Notes

Since Locally starts empty each time it's launched, after a restart the resources Aspire has stored in the AppHost's User Secrets will no longer exist. `AddLocally()` detects this and Aspire provisions them again - subsequent runs then reuse these, which means they start in a few seconds.

Reading from the earliest event each time keeps this example simple - however a real app would use an `EventProcessorClient` (with a checkpoint store) to only read each event once.

The AppHost intentionally doesn't call `RunAsEmulator()`, since the Event Hubs Namespace is provided by Locally - as such when run without `locally run`, `aspire run` provisions the Event Hubs Namespace in Azure, as it would for any other Aspire app.

To keep this example focused on Azure Event Hubs, the Api doesn't require authentication - however a real app would want to require this.

## Tearing it down

First stop the AppHost using `Ctrl+C`, then delete the Resource Group which Aspire created:

```bash
locally run az group delete --yes --name "$(locally run az group list \
  --query "[?starts_with(name, 'rg-aspire-locallyexamplesaspireeventhubsapphost-')].name | [0]" --output tsv)"
```
