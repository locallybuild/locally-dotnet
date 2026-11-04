# Locally.Aspire.Client

This package allows connecting the Azure SDK clients of [a (.Net) Aspire application](https://aspire.dev) application to [Locally Build](https://locally.build).

This can be done by adding the `builder.UseLocallyCredential()` method:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.UseLocallyCredential();

builder.AddAzureBlobContainerClient("entries");
builder.AddAzureQueue("submissions");
builder.AddRedisClientBuilder("cache").WithOutputCache();
```

And then launching your application via `locally run`, e.g.:

```
locally run dotnet run
```

This package can be used in conjunction with [`Locally.Aspire.Hosting`](https://github.com/locallybuild/locally-dotnet/tree/main/src/Locally.Aspire.Hosting), which allows you to provision the Azure resources that these clients need within Locally.

## What it does

* Support for configuring Instance Discovery via an environment variable: enables making this configurable via an environment variable (`AZURE_DISABLE_INSTANCE_DISCOVERY`). Instance Discovery is used to validate that an Azure Environment (like Azure Public, Azure China, etc) is known and operated by Microsoft (by reaching out to `login.microsoftonline.com`) - which isn't the case for a local Azure emulator like Locally Build (and so this needs to be disabled). The Azure.Identity library for .NET doesn't currently read this variable, however `LocallyCredential` does.
* Every Azure client: `UseLocallyCredential()` makes `LocallyCredential` the default credential for every client built by `Microsoft.Extensions.Azure` (which includes Aspire's Azure client integrations). Outside of `locally run` this is the same `DefaultAzureCredential` which these clients would have used anyway - which on App Service means the app's Managed Identity.
* Redis: Aspire's `WithAzureAuthentication()` only signs in to caches using Azure's own host names, as such `UseLocallyCredential()` signs in to Locally's caches using a Microsoft Entra token. This applies to any connection to a `*.locally` host which uses TLS and has no password.

## Using a real Azure resource

To use a real Azure resource alongside Locally, give that client its own credential - which takes precedence over `UseLocallyCredential()`, like so:

```csharp
var azure = new ClientSecretCredential(tenantId, clientId, clientSecret,
    new ClientSecretCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud });

builder.AddAzureBlobContainerClient("archive", settings => settings.Credential = azure);
```

Note that you'll need to specify Microsoft Entra ID's authority host (as above), since under `locally run` Azure.Identity's credentials sign in to wherever `AZURE_AUTHORITY_HOST` points (which is Locally) - and the same applies to the Azure CLI and `DefaultAzureCredential`.

## Clients it can't reach

Some clients don't use Microsoft.Extensions.Azure - as such you'll need to give these a `LocallyCredential` yourself:

```csharp
// Aspire's PostgreSQL, Cosmos DB and App Configuration clients create their own credential.
builder.AddAzureNpgsqlDataSource("db", settings => settings.Credential = new LocallyCredential());
builder.AddAzureCosmosClient("cosmos", settings => settings.Credential = new LocallyCredential());
builder.AddAzureAppConfiguration("config", settings => settings.Credential = new LocallyCredential());

// Clients you construct yourself.
var blobs = new BlobServiceClient(endpoint, new LocallyCredential());
```

`Microsoft.Data.SqlClient` signs in using a credential of its own when the connection string specifies `Authentication="Active Directory Default"` (as Aspire's Azure SQL connection strings do) - as such you'll want to remove that and sign in using a `LocallyCredential` instead:

```csharp
var credential = new LocallyCredential();
var connectionString = new SqlConnectionStringBuilder(builder.Configuration.GetConnectionString("db"));
connectionString.Remove("Authentication");

await using var connection = new SqlConnection(connectionString.ConnectionString)
{
    AccessTokenCallback = async (context, cancellationToken) =>
    {
        var token = await credential.GetTokenAsync(
            new TokenRequestContext([context.Resource.TrimEnd('/') + "/.default"]), cancellationToken);
        return new SqlAuthenticationToken(token.Token, token.ExpiresOn);
    },
};
```
