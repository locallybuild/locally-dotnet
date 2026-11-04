using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;

var builder = WebApplication.CreateBuilder(args);
builder.UseLocallyCredential();

builder.AddAzureBlobContainerClient("entries");
builder.AddAzureQueue("submissions");
builder.AddAzureTableServiceClient("tables");

var app = builder.Build();

// Stores an entry as a blob and a table row, and queues its id for processing.
app.MapPost("/entries/{id}", async (string id, HttpRequest request,
    BlobContainerClient entries, QueueClient submissions, TableServiceClient tables) =>
{
    using var reader = new StreamReader(request.Body);
    var text = await reader.ReadToEndAsync();

    await entries.UploadBlobAsync(id, BinaryData.FromString(text));

    var table = tables.GetTableClient("entries");
    await table.CreateIfNotExistsAsync();
    await table.UpsertEntityAsync(new TableEntity("entries", id) { ["Length"] = text.Length });

    await submissions.SendMessageAsync(id);

    return Results.Created($"/entries/{id}", null);
});

// Reads an entry back from the blob and the table row.
app.MapGet("/entries/{id}", async (string id, BlobContainerClient entries, TableServiceClient tables) =>
{
    var blob = await entries.GetBlobClient(id).DownloadContentAsync();
    var row = await tables.GetTableClient("entries").GetEntityAsync<TableEntity>("entries", id);

    return new Entry(id, blob.Value.Content.ToString(), row.Value.GetInt32("Length") ?? 0);
});

// Takes the next id off the queue.
app.MapPost("/submissions/next", async (QueueClient submissions) =>
{
    var message = (await submissions.ReceiveMessageAsync()).Value;
    if (message is null)
    {
        return Results.NoContent();
    }

    await submissions.DeleteMessageAsync(message.MessageId, message.PopReceipt);
    return Results.Ok(message.Body.ToString());
});

app.Run();

record Entry(string Id, string Text, int Length);
