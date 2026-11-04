using Azure.Security.KeyVault.Secrets;

var builder = WebApplication.CreateBuilder(args);
builder.UseLocallyCredential();

builder.AddAzureKeyVaultClient("secrets");

var app = builder.Build();

// Stores a secret in the Key Vault.
app.MapPut("/secrets/{name}", async (string name, HttpRequest request, SecretClient secrets) =>
{
    using var reader = new StreamReader(request.Body);
    await secrets.SetSecretAsync(name, await reader.ReadToEndAsync());
    return Results.NoContent();
});

// Reads a secret back from the Key Vault.
app.MapGet("/secrets/{name}", async (string name, SecretClient secrets) =>
    Results.Ok((await secrets.GetSecretAsync(name)).Value.Value));

app.Run();
