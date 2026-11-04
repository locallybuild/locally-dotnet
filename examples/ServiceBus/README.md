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

# Example: Use Azure Service Bus from an Aspire app within Locally

This example shows how to use Azure Service Bus (queues, topics and subscriptions) from an [Aspire](https://aspire.dev) app on [Locally Build](https://locally.build).

When the AppHost is run using `locally run aspire run`, Aspire provisions the Service Bus Namespace in Locally (rather than in Azure) and grants you access to it, just as it would in an Azure Subscription. The Api then signs in to the Service Bus Namespace using the identity which `locally run` provides - as such there's no need for any Access Keys, or Connection Strings containing secrets.

```
 curl ─▶ api ──┬─ POST /orders/{id}        ──▶ Queue (orders)
               ├─ POST /orders/next        ◀── Queue (orders)
               ├─ POST /events/{name}      ──▶ Topic (events)
               └─ POST /events/audit/next  ◀── Subscription (events/audit)
```

This example is made up of three projects, each written in C# using .NET 10:

* [`Locally.Examples.Aspire.ServiceBus.AppHost`](./Locally.Examples.Aspire.ServiceBus.AppHost/Program.cs) - the AppHost, which defines a Service Bus Namespace containing the `orders` Queue and the `events` Topic (with an `audit` Subscription) - and references it from the Api.
* [`Locally.Examples.Aspire.ServiceBus.Api`](./Locally.Examples.Aspire.ServiceBus.Api/Program.cs) - the Api, which sends orders to the Queue and publishes events to the Topic - and takes the next message off each of them using `POST /orders/next` and `POST /events/audit/next`.
* [`Locally.Examples.Aspire.ServiceBus.Tests`](./Locally.Examples.Aspire.ServiceBus.Tests/) - the tests, which start the AppHost using `Aspire.Hosting.Testing`, send an order and an event using the Api and then check that each of them is received (this is what runs in GitHub Actions).

## The Aspire packages

| Package | Used in | What it does here |
| --- | --- | --- |
| [`Aspire.Hosting.Azure.ServiceBus`](https://www.nuget.org/packages/Aspire.Hosting.Azure.ServiceBus) | AppHost | Defines the Service Bus Namespace, along with the `orders` Queue, the `events` Topic and its `audit` Subscription. |
| [`Locally.Aspire.Hosting`](../../src/Locally.Aspire.Hosting) | AppHost | `builder.AddLocally()` - which means that when run using `locally run`, `aspire run` provisions the Service Bus Namespace in Locally rather than in Azure. |
| [`Aspire.Azure.Messaging.ServiceBus`](https://www.nuget.org/packages/Aspire.Azure.Messaging.ServiceBus) | Api | Configures the Service Bus client using the Connection String which the AppHost passes to the Api. |
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
* The Locally Plugin for `Microsoft.ServiceBus` installed (`locally plugin install --name Microsoft.ServiceBus`).

## Running the example

First, make sure your container runtime (Docker or Podman) is running, then start Locally:

```bash
locally build
```

Once Locally is running, open another terminal and run the AppHost:

```bash
cd examples/ServiceBus
locally run aspire run --apphost Locally.Examples.Aspire.ServiceBus.AppHost
```

The first time this is run, Aspire creates a Resource Group named `rg-aspire-locallyexamplesaspireservicebusapphost-<hash>`, provisions the resources within it and then grants your `locally run` identity (which the Api signs in as) access to them. Aspire then stores the details of what it's provisioned in the AppHost's User Secrets, and reuses these on subsequent runs.

Once it's running, Aspire outputs a link to the Aspire Dashboard - and the Api is available at `http://localhost:5082`.

To send an order to the Queue:

```bash
curl -i -X POST http://localhost:5082/orders/first
```

```
HTTP/1.1 202 Accepted
```

Which can then be taken off the Queue - once the Queue is empty this returns a `204 No Content`:

```bash
curl -X POST http://localhost:5082/orders/next
```

```json
"first"
```

Similarly, to publish an event to the Topic and then take it off the `audit` Subscription:

```bash
curl -i -X POST http://localhost:5082/events/signed-up
curl -X POST http://localhost:5082/events/audit/next
```

```json
"signed-up"
```

### Running the tests

Since the tests start the AppHost themselves, there's no need to run `aspire run` first - as such these can be run using:

```bash
cd examples/ServiceBus
locally run dotnet test Locally.Examples.Aspire.ServiceBus.slnx
```

## Notes

Since Locally starts empty each time it's launched, after a restart the resources Aspire has stored in the AppHost's User Secrets will no longer exist. `AddLocally()` detects this and Aspire provisions them again - subsequent runs then reuse these, which means they start in a few seconds.

The AppHost intentionally doesn't call `RunAsEmulator()`, since the Service Bus Namespace is provided by Locally - as such when run without `locally run`, `aspire run` provisions the Service Bus Namespace in Azure, as it would for any other Aspire app.

To keep this example focused on Azure Service Bus, the Api doesn't require authentication - however a real app would want to require this.

## Tearing it down

First stop the AppHost using `Ctrl+C`, then delete the Resource Group which Aspire created:

```bash
locally run az group delete --yes --name "$(locally run az group list \
  --query "[?starts_with(name, 'rg-aspire-locallyexamplesaspireservicebusapphost-')].name | [0]" --output tsv)"
```
