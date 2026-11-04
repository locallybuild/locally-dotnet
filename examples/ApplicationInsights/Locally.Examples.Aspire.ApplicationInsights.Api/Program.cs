using Azure.Monitor.OpenTelemetry.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.UseLocallyCredential();

// Reads APPLICATIONINSIGHTS_CONNECTION_STRING, which the AppHost's reference sets.
builder.Services.AddOpenTelemetry().UseAzureMonitor();

var app = builder.Build();

// Logs a greeting, which is sent to Application Insights along with the request.
app.MapGet("/hello/{name}", (string name, ILogger<Program> logger) =>
{
    logger.LogInformation("Saying hello to {Name}", name);
    return $"Hello, {name}";
});

app.Run();
