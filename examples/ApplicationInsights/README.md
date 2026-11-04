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

# Example: Use Application Insights from an Aspire app within Locally

This example shows how to send telemetry from an [Aspire](https://aspire.dev) app to Application Insights (backed by a Log Analytics Workspace) on [Locally Build](https://locally.build).

When the AppHost is run using `locally run aspire run`, Aspire provisions the Log Analytics Workspace and the Application Insights Component in Locally (rather than in Azure), and passes the Application Insights Connection String to the Api - which then sends its telemetry to Locally rather than to Azure.

```
 curl ─▶ api ── GET /hello/{name}
            │
            └─ OpenTelemetry ──▶ Application Insights ──▶ Log Analytics Workspace
```

This example is made up of three projects, each written in C# using .NET 10:

* [`Locally.Examples.Aspire.ApplicationInsights.AppHost`](./Locally.Examples.Aspire.ApplicationInsights.AppHost/Program.cs) - the AppHost, which defines a Log Analytics Workspace and an Application Insights Component which uses it - and references Application Insights from the Api.
* [`Locally.Examples.Aspire.ApplicationInsights.Api`](./Locally.Examples.Aspire.ApplicationInsights.Api/Program.cs) - the Api, which sends its telemetry to Application Insights using the Azure Monitor OpenTelemetry Distro - and logs a message for each request to `GET /hello/{name}`.
* [`Locally.Examples.Aspire.ApplicationInsights.Tests`](./Locally.Examples.Aspire.ApplicationInsights.Tests/) - the tests, which start the AppHost using `Aspire.Hosting.Testing` and then check that the Api starts and responds (since `UseAzureMonitor()` throws at startup when there's no Connection String) (this is what runs in GitHub Actions).

## The Aspire packages

| Package | Used in | What it does here |
| --- | --- | --- |
| [`Aspire.Hosting.Azure.ApplicationInsights`](https://www.nuget.org/packages/Aspire.Hosting.Azure.ApplicationInsights), [`Aspire.Hosting.Azure.OperationalInsights`](https://www.nuget.org/packages/Aspire.Hosting.Azure.OperationalInsights) | AppHost | Defines the Application Insights Component and the Log Analytics Workspace. |
| [`Locally.Aspire.Hosting`](../../src/Locally.Aspire.Hosting) | AppHost | `builder.AddLocally()` - which means that when run using `locally run`, `aspire run` provisions these in Locally rather than in Azure. |
| [`Azure.Monitor.OpenTelemetry.AspNetCore`](https://www.nuget.org/packages/Azure.Monitor.OpenTelemetry.AspNetCore) | Api | Sends the Api's telemetry to Application Insights, using the `APPLICATIONINSIGHTS_CONNECTION_STRING` which the AppHost passes to the Api. |
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
* The Locally Plugin for `Microsoft.Insights` installed (`locally plugin install --name Microsoft.Insights`).
* The Locally Plugin for `Microsoft.OperationalInsights` installed (`locally plugin install --name Microsoft.OperationalInsights`).

## Running the example

First, make sure your container runtime (Docker or Podman) is running, then start Locally:

```bash
locally build
```

Once Locally is running, open another terminal and run the AppHost:

```bash
cd examples/ApplicationInsights
locally run aspire run --apphost Locally.Examples.Aspire.ApplicationInsights.AppHost
```

The first time this is run, Aspire creates a Resource Group named `rg-aspire-locallyexamplesaspireapplicationinsightsapphost-<hash>`, provisions the resources within it and then grants your `locally run` identity (which the Api signs in as) access to them. Aspire then stores the details of what it's provisioned in the AppHost's User Secrets, and reuses these on subsequent runs.

Once it's running, Aspire outputs a link to the Aspire Dashboard - and the Api is available at `http://localhost:5085`.

To make a request (which is sent to Application Insights):

```bash
curl http://localhost:5085/hello/Locally
```

```
Hello, Locally
```

The telemetry can then be queried using the Azure CLI, like so:

```bash
APP_ID=$(locally run az monitor app-insights component show \
  --query "[?starts_with(resourceGroup, 'rg-aspire-locallyexamplesaspireapplicationinsightsapphost-')].appId | [0]" --output tsv)
locally run az monitor app-insights query --app "$APP_ID" \
  --analytics-query 'requests | project timestamp, name, resultCode | order by timestamp desc' \
  --query 'tables[0].rows' --output tsv
```

Which lists the requests the Api has served:

```
2026-11-04T11:22:36Z	GET /hello/{name}	200
2026-11-04T11:22:06Z	GET /hello/{name}	200
```

### Running the tests

Since the tests start the AppHost themselves, there's no need to run `aspire run` first - as such these can be run using:

```bash
cd examples/ApplicationInsights
locally run dotnet test Locally.Examples.Aspire.ApplicationInsights.slnx
```

## Notes

Since Locally starts empty each time it's launched, after a restart the resources Aspire has stored in the AppHost's User Secrets will no longer exist. `AddLocally()` detects this and Aspire provisions them again - subsequent runs then reuse these, which means they start in a few seconds.

The Azure Monitor exporter batches telemetry before sending it, as such it can take a few seconds for a request to show up in Application Insights.

To keep this example focused on Application Insights, the Api doesn't require authentication - however a real app would want to require this.

## Tearing it down

First stop the AppHost using `Ctrl+C`, then delete the Resource Group which Aspire created:

```bash
locally run az group delete --yes --name "$(locally run az group list \
  --query "[?starts_with(name, 'rg-aspire-locallyexamplesaspireapplicationinsightsapphost-')].name | [0]" --output tsv)"
```
