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

# Example: Use Azure Database for PostgreSQL from an Aspire app within Locally

This example shows how to use an Azure Database for PostgreSQL Flexible Server from an [Aspire](https://aspire.dev) app on [Locally Build](https://locally.build).

When the AppHost is run using `locally run aspire run`, Aspire provisions the Flexible Server and its Database in Locally (rather than in Azure) and grants you access to them, just as it would in an Azure Subscription. The Api then signs in to the Database using the identity which `locally run` provides - as such there's no need for any passwords, or Connection Strings containing secrets.

```
 curl ─▶ api ──┬─ PUT /notes/{id} ──▶ PostgreSQL Flexible Server (db, notes table)
               └─ GET /notes/{id} ◀── PostgreSQL Flexible Server (db, notes table)
```

This example is made up of three projects, each written in C# using .NET 10:

* [`Locally.Examples.Aspire.PostgreSQL.AppHost`](./Locally.Examples.Aspire.PostgreSQL.AppHost/Program.cs) - the AppHost, which defines a PostgreSQL Flexible Server containing the `db` Database - and references it from the Api.
* [`Locally.Examples.Aspire.PostgreSQL.Api`](./Locally.Examples.Aspire.PostgreSQL.Api/Program.cs) - the Api, which creates the `notes` table when it starts, stores notes in it using `PUT /notes/{id}` and reads them back using `GET /notes/{id}`.
* [`Locally.Examples.Aspire.PostgreSQL.Tests`](./Locally.Examples.Aspire.PostgreSQL.Tests/) - the tests, which start the AppHost using `Aspire.Hosting.Testing`, store a note using the Api and then check that it's read back from the Database (this is what runs in GitHub Actions).

## The Aspire packages

| Package | Used in | What it does here |
| --- | --- | --- |
| [`Aspire.Hosting.Azure.PostgreSQL`](https://www.nuget.org/packages/Aspire.Hosting.Azure.PostgreSQL) | AppHost | Defines the PostgreSQL Flexible Server and the `db` Database. |
| [`Locally.Aspire.Hosting`](../../src/Locally.Aspire.Hosting) | AppHost | `builder.AddLocally()` - which means that when run using `locally run`, `aspire run` provisions the Flexible Server in Locally rather than in Azure. |
| [`Aspire.Azure.Npgsql`](https://www.nuget.org/packages/Aspire.Azure.Npgsql) | Api | Configures an `NpgsqlDataSource` using the Connection String which the AppHost passes to the Api, signing in using Microsoft Entra ID. |
| [`Locally.Aspire.Client`](../../src/Locally.Aspire.Client) | Api | `new LocallyCredential()` - which the data source is given to sign in to Locally (see below). |

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
* The Locally Plugin for `Microsoft.DBforPostgreSQL` installed (`locally plugin install --name Microsoft.DBforPostgreSQL`).

## Running the example

First, make sure your container runtime (Docker or Podman) is running, then start Locally:

```bash
locally build
```

Once Locally is running, open another terminal and run the AppHost:

```bash
cd examples/PostgreSQL
locally run aspire run --apphost Locally.Examples.Aspire.PostgreSQL.AppHost
```

The first time this is run, Aspire creates a Resource Group named `rg-aspire-locallyexamplesaspirepostgresqlapphost-<hash>`, provisions the resources within it and then grants your `locally run` identity (which the Api signs in as) access to them. Aspire then stores the details of what it's provisioned in the AppHost's User Secrets, and reuses these on subsequent runs.

Once it's running, Aspire outputs a link to the Aspire Dashboard - and the Api is available at `http://localhost:5084`.

To store a note:

```bash
curl -i -X PUT -d 'hello from Locally' http://localhost:5084/notes/first
```

```
HTTP/1.1 204 No Content
```

Which can then be read back from the Database:

```bash
curl http://localhost:5084/notes/first
```

```json
"hello from Locally"
```

### Running the tests

Since the tests start the AppHost themselves, there's no need to run `aspire run` first - as such these can be run using:

```bash
cd examples/PostgreSQL
locally run dotnet test Locally.Examples.Aspire.PostgreSQL.slnx
```

## Notes

Since Locally starts empty each time it's launched, after a restart the resources Aspire has stored in the AppHost's User Secrets will no longer exist. `AddLocally()` detects this and Aspire provisions them again - subsequent runs then reuse these, which means they start in a few seconds.

Aspire's PostgreSQL data source creates a credential of its own rather than using the one which `UseLocallyCredential()` configures - as such the Api gives it a `LocallyCredential`, like so:

```csharp
builder.AddAzureNpgsqlDataSource("db", settings => settings.Credential = new LocallyCredential());
```

The AppHost intentionally doesn't call `RunAsContainer()`, since the Flexible Server is provided by Locally - as such when run without `locally run`, `aspire run` provisions the Flexible Server in Azure, as it would for any other Aspire app.

To keep this example focused on Azure Database for PostgreSQL, the Api doesn't require authentication - however a real app would want to require this.

## Tearing it down

First stop the AppHost using `Ctrl+C`, then delete the Resource Group which Aspire created:

```bash
locally run az group delete --yes --name "$(locally run az group list \
  --query "[?starts_with(name, 'rg-aspire-locallyexamplesaspirepostgresqlapphost-')].name | [0]" --output tsv)"
```
