using Locally.Aspire.Client;
using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);
builder.UseLocallyCredential();

// Aspire's Cosmos DB client creates a credential of its own, so it needs to be given a LocallyCredential.
builder.AddAzureCosmosClient("cosmos", settings => settings.Credential = new LocallyCredential());

var app = builder.Build();

// Stores an item in the container.
app.MapPut("/items/{id}", async (string id, HttpRequest request, CosmosClient cosmos) =>
{
    using var reader = new StreamReader(request.Body);
    var items = cosmos.GetContainer("app", "items");
    await items.UpsertItemAsync(new Item(id, await reader.ReadToEndAsync()), new PartitionKey(id));
    return Results.NoContent();
});

// Reads an item back from the container.
app.MapGet("/items/{id}", async (string id, CosmosClient cosmos) =>
{
    var items = cosmos.GetContainer("app", "items");
    return (await items.ReadItemAsync<Item>(id, new PartitionKey(id))).Resource;
});

app.Run();

record Item(string id, string text);
