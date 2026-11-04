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

# Example: Use Azure SQL from an Aspire app within Locally

This example shows how to use an Azure SQL Database from an [Aspire](https://aspire.dev) app on [Locally Build](https://locally.build).

When the AppHost is run using `locally run aspire run`, Aspire provisions the SQL Server and its Database in Locally (rather than in Azure) and grants you access to them, just as it would in an Azure Subscription. The Api then signs in to the Database using the identity which `locally run` provides - as such there's no need for any passwords, or Connection Strings containing secrets.

```
 curl ─▶ api ──┬─ PUT /notes/{id} ──▶ Azure SQL (db, notes table)
               └─ GET /notes/{id} ◀── Azure SQL (db, notes table)
```

This example is made up of three projects, each written in C# using .NET 10:

* [`Locally.Examples.Aspire.AzureSQL.AppHost`](./Locally.Examples.Aspire.AzureSQL.AppHost/Program.cs) - the AppHost, which defines a SQL Server containing the `db` Database - and references it from the Api.
* [`Locally.Examples.Aspire.AzureSQL.Api`](./Locally.Examples.Aspire.AzureSQL.Api/Program.cs) - the Api, which creates the `notes` table when it starts, stores notes in it using `PUT /notes/{id}` and reads them back using `GET /notes/{id}`.
* [`Locally.Examples.Aspire.AzureSQL.Tests`](./Locally.Examples.Aspire.AzureSQL.Tests/) - the tests, which start the AppHost using `Aspire.Hosting.Testing`, store a note using the Api and then check that it's read back from the Database (this is what runs in GitHub Actions).

## The Aspire packages

| Package | Used in | What it does here |
| --- | --- | --- |
| [`Aspire.Hosting.Azure.Sql`](https://www.nuget.org/packages/Aspire.Hosting.Azure.Sql) | AppHost | Defines the SQL Server and the `db` Database. |
| [`Locally.Aspire.Hosting`](../../src/Locally.Aspire.Hosting) | AppHost | `builder.AddLocally()` - which means that when run using `locally run`, `aspire run` provisions the SQL Server in Locally rather than in Azure. |
| [`Microsoft.Data.SqlClient`](https://www.nuget.org/packages/Microsoft.Data.SqlClient) | Api | Connects to the Database using the Connection String which the AppHost passes to the Api. |
| [`Locally.Aspire.Client`](../../src/Locally.Aspire.Client) | Api | `new LocallyCredential()` - which provides the access token SqlClient signs in with (see below). |

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
* The Locally Plugin for `Microsoft.Sql` installed (`locally plugin install --name Microsoft.Sql --accept-sql-server-licence`).

## Running the example

First, make sure your container runtime (Docker or Podman) is running, then start Locally:

```bash
locally build
```

Once Locally is running, open another terminal and run the AppHost:

```bash
cd examples/AzureSQL
locally run aspire run --apphost Locally.Examples.Aspire.AzureSQL.AppHost
```

The first time this is run, Aspire creates a Resource Group named `rg-aspire-locallyexamplesaspireazuresqlapphost-<hash>`, provisions the resources within it and then grants your `locally run` identity (which the Api signs in as) access to them. Aspire then stores the details of what it's provisioned in the AppHost's User Secrets, and reuses these on subsequent runs.

Once it's running, Aspire outputs a link to the Aspire Dashboard - and the Api is available at `http://localhost:5088`.

To store a note:

```bash
curl -i -X PUT -d 'hello from Locally' http://localhost:5088/notes/first
```

```
HTTP/1.1 204 No Content
```

Which can then be read back from the Database:

```bash
curl http://localhost:5088/notes/first
```

```json
"hello from Locally"
```

### Running the tests

Since the tests start the AppHost themselves, there's no need to run `aspire run` first - as such these can be run using:

```bash
cd examples/AzureSQL
locally run dotnet test Locally.Examples.Aspire.AzureSQL.slnx
```

## Notes

Since Locally starts empty each time it's launched, after a restart the resources Aspire has stored in the AppHost's User Secrets will no longer exist. `AddLocally()` detects this and Aspire provisions them again - subsequent runs then reuse these, which means they start in a few seconds.

Aspire's Connection String for the Database sets `Authentication="Active Directory Default"`, which has SqlClient create a credential of its own rather than using the one which `UseLocallyCredential()` configures (and which contacts `login.microsoftonline.com`) - as such the Api removes this, and provides an access token from a `LocallyCredential` instead:

```csharp
var credential = new LocallyCredential();
var connectionString = new SqlConnectionStringBuilder(builder.Configuration.GetConnectionString("db"));
connectionString.Remove("Authentication");

var connection = new SqlConnection(connectionString.ConnectionString)
{
    AccessTokenCallback = async (context, cancellationToken) =>
    {
        var token = await credential.GetTokenAsync(
            new TokenRequestContext([context.Resource.TrimEnd('/') + "/.default"]), cancellationToken);
        return new SqlAuthenticationToken(token.Token, token.ExpiresOn);
    },
};
```

The Azure SQL plugin runs SQL Server, as such installing it requires accepting the SQL Server licence (`--accept-sql-server-licence`).

The AppHost intentionally doesn't call `RunAsContainer()`, since the SQL Server is provided by Locally - as such when run without `locally run`, `aspire run` provisions the SQL Server in Azure, as it would for any other Aspire app.

To keep this example focused on Azure SQL, the Api doesn't require authentication - however a real app would want to require this.

## Tearing it down

First stop the AppHost using `Ctrl+C`, then delete the Resource Group which Aspire created:

```bash
locally run az group delete --yes --name "$(locally run az group list \
  --query "[?starts_with(name, 'rg-aspire-locallyexamplesaspireazuresqlapphost-')].name | [0]" --output tsv)"
```
