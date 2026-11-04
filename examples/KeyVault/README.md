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

# Example: Use Azure Key Vault from an Aspire app within Locally

This example shows how to use secrets in Azure Key Vault from an [Aspire](https://aspire.dev) app on [Locally Build](https://locally.build).

When the AppHost is run using `locally run aspire run`, Aspire provisions the Key Vault in Locally (rather than in Azure) and grants you access to it, just as it would in an Azure Subscription. The Api then signs in to the Key Vault using the identity which `locally run` provides.

```
 curl ─▶ api ──┬─ PUT /secrets/{name} ──▶ Key Vault (secrets)
               └─ GET /secrets/{name} ◀── Key Vault (secrets)
```

This example is made up of three projects, each written in C# using .NET 10:

* [`Locally.Examples.Aspire.KeyVault.AppHost`](./Locally.Examples.Aspire.KeyVault.AppHost/Program.cs) - the AppHost, which defines a Key Vault and references it from the Api.
* [`Locally.Examples.Aspire.KeyVault.Api`](./Locally.Examples.Aspire.KeyVault.Api/Program.cs) - the Api, which stores secrets in the Key Vault using `PUT /secrets/{name}`, and reads them back using `GET /secrets/{name}`.
* [`Locally.Examples.Aspire.KeyVault.Tests`](./Locally.Examples.Aspire.KeyVault.Tests/) - the tests, which start the AppHost using `Aspire.Hosting.Testing`, store a secret using the Api and then check that it's read back from the Key Vault (this is what runs in GitHub Actions).

## The Aspire packages

| Package | Used in | What it does here |
| --- | --- | --- |
| [`Aspire.Hosting.Azure.KeyVault`](https://www.nuget.org/packages/Aspire.Hosting.Azure.KeyVault) | AppHost | Defines the Key Vault. |
| [`Locally.Aspire.Hosting`](../../src/Locally.Aspire.Hosting) | AppHost | `builder.AddLocally()` - which means that when run using `locally run`, `aspire run` provisions the Key Vault in Locally rather than in Azure. |
| [`Aspire.Azure.Security.KeyVault`](https://www.nuget.org/packages/Aspire.Azure.Security.KeyVault) | Api | Configures the Key Vault Secrets client using the Key Vault URI which the AppHost passes to the Api. |
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
* The Locally Plugin for `Microsoft.KeyVault` installed (`locally plugin install --name Microsoft.KeyVault`).

## Running the example

First, make sure your container runtime (Docker or Podman) is running, then start Locally:

```bash
locally build
```

Once Locally is running, open another terminal and run the AppHost:

```bash
cd examples/KeyVault
locally run aspire run --apphost Locally.Examples.Aspire.KeyVault.AppHost
```

The first time this is run, Aspire creates a Resource Group named `rg-aspire-locallyexamplesaspirekeyvaultapphost-<hash>`, provisions the resources within it and then grants your `locally run` identity (which the Api signs in as) access to them. Aspire then stores the details of what it's provisioned in the AppHost's User Secrets, and reuses these on subsequent runs.

Once it's running, Aspire outputs a link to the Aspire Dashboard - and the Api is available at `http://localhost:5083`.

To store a secret:

```bash
curl -i -X PUT -d 'hello from Locally' http://localhost:5083/secrets/greeting
```

```
HTTP/1.1 204 No Content
```

Which can then be read back from the Key Vault:

```bash
curl http://localhost:5083/secrets/greeting
```

```json
"hello from Locally"
```

### Running the tests

Since the tests start the AppHost themselves, there's no need to run `aspire run` first - as such these can be run using:

```bash
cd examples/KeyVault
locally run dotnet test Locally.Examples.Aspire.KeyVault.slnx
```

## Notes

Since Locally starts empty each time it's launched, after a restart the resources Aspire has stored in the AppHost's User Secrets will no longer exist. `AddLocally()` detects this and Aspire provisions them again - subsequent runs then reuse these, which means they start in a few seconds.

Key Vault doesn't have an emulator, as such when run without `locally run`, `aspire run` provisions the Key Vault in Azure, as it would for any other Aspire app.

To keep this example focused on Azure Key Vault, the Api doesn't require authentication - however a real app would want to require this.

## Tearing it down

First stop the AppHost using `Ctrl+C`, then delete the Resource Group which Aspire created:

```bash
locally run az group delete --yes --name "$(locally run az group list \
  --query "[?starts_with(name, 'rg-aspire-locallyexamplesaspirekeyvaultapphost-')].name | [0]" --output tsv)"
```
