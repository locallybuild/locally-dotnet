using Azure.Data.AppConfiguration;
using Locally.Aspire.Client;

var builder = WebApplication.CreateBuilder(args);
builder.UseLocallyCredential();

// Aspire's App Configuration provider creates a credential of its own, so it needs to be given a
// LocallyCredential - without one, Optional = true fails silently and the app starts with no settings.
builder.AddAzureAppConfiguration(
    "config",
    settings => settings.Credential = new LocallyCredential(),
    options => options.ConfigureRefresh(refresh => refresh.RegisterAll().SetRefreshInterval(TimeSpan.FromSeconds(1))));
builder.Services.AddAzureAppConfiguration();

// A client for writing settings, which (since it's created here) also needs to be given a LocallyCredential.
var endpoint = new Uri(builder.Configuration.GetConnectionString("config")!);
builder.Services.AddSingleton(new ConfigurationClient(endpoint, new LocallyCredential()));

var app = builder.Build();

// Refreshes the app's configuration from App Configuration as requests come in.
app.UseAzureAppConfiguration();

// Stores a setting in App Configuration.
app.MapPut("/settings/{key}", async (string key, HttpRequest request, ConfigurationClient client) =>
{
    using var reader = new StreamReader(request.Body);
    await client.SetConfigurationSettingAsync(key, await reader.ReadToEndAsync());
    return Results.NoContent();
});

// Reads a setting from the app's configuration, which App Configuration provides.
app.MapGet("/settings/{key}", (string key, IConfiguration configuration) =>
    configuration[key] is string value ? Results.Ok(value) : Results.NotFound());

app.Run();
