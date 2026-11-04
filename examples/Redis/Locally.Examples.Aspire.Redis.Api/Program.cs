using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.UseLocallyCredential();

// WithAzureAuthentication() signs in to caches in Azure, whilst UseLocallyCredential() signs in to those in Locally.
builder.AddRedisClientBuilder("cache")
    .WithAzureAuthentication();

var app = builder.Build();

// Stores a value in the cache.
app.MapPut("/values/{key}", async (string key, HttpRequest request, IConnectionMultiplexer redis) =>
{
    using var reader = new StreamReader(request.Body);
    await redis.GetDatabase().StringSetAsync(key, await reader.ReadToEndAsync());
    return Results.NoContent();
});

// Reads a value back from the cache.
app.MapGet("/values/{key}", async (string key, IConnectionMultiplexer redis) =>
{
    var value = await redis.GetDatabase().StringGetAsync(key);
    return value.IsNull ? Results.NotFound() : Results.Ok(value.ToString());
});

app.Run();
