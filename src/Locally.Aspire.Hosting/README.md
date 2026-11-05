# Locally.Aspire.Hosting

Runs an [Aspire](https://aspire.dev) AppHost's Azure resources in [Locally](https://locally.build) - when run under `locally run`, `aspire run` and `aspire deploy` provision these into Locally rather than into Azure. Aspire creates them, grants you access to them and hands your projects their connection strings, just as it would in an Azure Subscription.

```csharp
var builder = DistributedApplication.CreateBuilder(args);
builder.AddLocally();

var storage = builder.AddAzureStorage("storage");
// ...
```

```bash
locally run aspire run
```

The same goes for deploying the whole AppHost, including its projects, which is useful to try out a deployment to App Service before deploying it to Azure:

```bash
locally run aspire deploy
```

When deploying under `locally run`, the projects' container images are built for your machine's architecture rather than for `linux/amd64`, since Locally runs them on your machine (where .NET hangs under emulation). The deployment summary which Aspire prints still shows `*.azurewebsites.net` addresses and Azure Portal links, since Aspire builds these itself rather than reading them from the deployment.

Outside of `locally run`, the only thing `AddLocally()` changes is the [host names and ports](#host-names-and-ports) which Aspire writes into its Bicep - and since on Azure these come out exactly as Aspire would have written them, the same AppHost publishes and runs against Azure as it did before.

## Emulators and containers

Aspire never provisions a resource which is set to `RunAsEmulator()` or `RunAsContainer()` - as such you'll want to leave those calls out for the resources Locally should provide, whilst keeping them on any resources you'd like to stay local.

To only use emulators when you're not running under Locally, you can check `IsLocallyRun()`, like so:

```csharp
var storage = builder.AddAzureStorage("storage");
if (!builder.IsLocallyRun())
{
    storage.RunAsEmulator();
}
```

## What it changes

All of these only apply when running under `locally run`:

* Azure Resource Manager endpoint: Aspire always calls `management.azure.com` and there's no setting to change that - however since every Azure SDK client starts from `ClientOptions.Default`, a pipeline policy added there sends Aspire's requests to Locally instead.
* Sign-in: Aspire signs in using the registered `ITokenCredentialProvider`, as such `AddLocally()` registers one for the `locally run` identity with instance discovery turned off (which means nothing contacts `login.microsoftonline.com`).
* Token Audiences: Aspire requests tokens for Azure's audiences (`https://management.azure.com`, and the legacy Azure AD Graph, which it uses to work out who you are) - the credential asks Locally for its own audiences instead.
* Subscription, Tenant and Region: these come from `locally run`, overriding any which Aspire saved during an earlier run.
* Saved state after a restart: Aspire saves what it's provisioned into the AppHost's user secrets, and reuses this without checking that it still exists. Since Locally starts empty each time it's launched, when the saved Resource Group no longer exists `AddLocally()` discards that state and Aspire provisions everything again.
* Storage health checks: Aspire checks a Storage Account's blobs and queues from the AppHost using a credential of its own, which'll be a Managed Identity since `locally run` sets `AZURE_CLIENT_ID`. As such `AddLocally()` swaps those checks for ones which sign in as the `locally run` identity, whilst an emulated account keeps Aspire's own check.
* Portal links: the dashboard links each provisioned resource to the Azure Portal - `AddLocally()` points these links at the same resource in the Locally Dashboard instead, which is `https://localhost:5678` unless overridden using `LOCALLY_DASHBOARD_URL`.

## Host names and ports

The Bicep which Aspire publishes contains a few values which assume Azure's fixed host names and ports - since these differ in Locally, `AddLocally()` updates each of these to read the real value from the resource instead (both when publishing and under `locally run`). On Azure the result is exactly what Aspire would have written:

* App Service service discovery: a project's references to other App Service apps (e.g. `services__api__https__0` and `API_HTTPS`) are built as `https://<name>.azurewebsites.net` - instead these use the referenced site's `defaultHostName`.
* The Aspire dashboard on App Service: its URL (which the apps send telemetry to) uses the dashboard site's `defaultHostName`.
* Azure Managed Redis: Aspire writes port 10000 into the connection string - instead this uses the database's deployed `port`.
* Azure Cache for Redis: Aspire's connection string doesn't specify a port (relying on 6380) - instead this uses the cache's `sslPort`.

These don't require any changes to your AppHost.

## Your projects

Your projects also run under `locally run` - as such to sign their Azure clients in to Locally you'll want to use [`Locally.Aspire.Client`](https://github.com/locallybuild/locally-dotnet/tree/main/src/Locally.Aspire.Client):

```csharp
builder.UseLocallyCredential();
```

## Supported integrations

These have been tested against Locally by provisioning each integration under `locally run` (with a project doing a real write and read through its Aspire client integration), then publishing it using `aspire publish` and deploying it using `locally deploy`. This was last tested using Aspire 13.6.4 - and the `locally run` half of this is covered by [the examples](https://github.com/locallybuild/locally-dotnet/tree/main/examples), which run in GitHub Actions.

The following work in both:

* Storage (blobs, queues and tables)
* Azure Managed Redis (`AddAzureManagedRedis`) and Azure Cache for Redis (`AddAzureRedis`)
* Service Bus (queues, topics and subscriptions)
* Key Vault (secrets)
* Application Insights and Log Analytics

The following work, with a caveat:

* PostgreSQL Flexible Server, Cosmos DB and App Configuration: these provision and deploy, however their Aspire client integrations build a credential of their own which `UseLocallyCredential()` can't reach - as such under `locally run` you'll need to give each one `settings.Credential = new LocallyCredential()` ([see `Locally.Aspire.Client`](https://github.com/locallybuild/locally-dotnet/tree/main/src/Locally.Aspire.Client#clients-it-cant-reach)), otherwise App Configuration with `Optional = true` fails silently with empty configuration.
* Azure SQL: since `Microsoft.Data.SqlClient` builds a credential of its own from `Authentication="Active Directory Default"` (which `UseLocallyCredential()` can't reach, and which contacts login.microsoftonline.com) you'll need to give the connection a token from `LocallyCredential` ([see `Locally.Aspire.Client`](https://github.com/locallybuild/locally-dotnet/tree/main/src/Locally.Aspire.Client#clients-it-cant-reach)). At this time this has only been tested under `locally run`, rather than with `aspire publish` and `locally deploy`.
* Event Hubs: sending, receiving and reading the hub's properties work under `locally run`, and the Bicep from `aspire publish` deploys using `locally deploy` - however an app deployed into Locally hasn't been tested against it yet.
* AI Search: the service provisions and deploys, but since Locally doesn't have a Search data plane yet, clients can't use it.
* Container Registry and Virtual Network: these deploy from `aspire publish`, however since Aspire (as of 13.6.4) leaves them out of `aspire run` they're never provisioned under `locally run` (in either Azure or Locally).
* App Service and Container Apps environments: App Service environments deploy, however Container Apps environments don't yet, since Locally doesn't support the dashboard component which Aspire adds to them.

The following don't work in Locally at this time:

* Azure OpenAI
* SignalR
* Web PubSub
