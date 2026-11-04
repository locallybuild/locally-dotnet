using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateBuilder(args);
builder.UseLocallyCredential();

builder.AddAzureServiceBusClient("messaging");

var app = builder.Build();

// Sends an order to the queue.
app.MapPost("/orders/{id}", async (string id, ServiceBusClient client) =>
{
    await using var sender = client.CreateSender("orders");
    await sender.SendMessageAsync(new ServiceBusMessage(id));
    return Results.Accepted();
});

// Takes the next order off the queue.
app.MapPost("/orders/next", async (ServiceBusClient client) =>
{
    await using var receiver = client.CreateReceiver("orders");
    return await ReceiveNextAsync(receiver);
});

// Publishes an event to the topic.
app.MapPost("/events/{name}", async (string name, ServiceBusClient client) =>
{
    await using var sender = client.CreateSender("events");
    await sender.SendMessageAsync(new ServiceBusMessage(name));
    return Results.Accepted();
});

// Takes the next event off the topic's audit subscription.
app.MapPost("/events/audit/next", async (ServiceBusClient client) =>
{
    await using var receiver = client.CreateReceiver("events", "audit");
    return await ReceiveNextAsync(receiver);
});

app.Run();

static async Task<IResult> ReceiveNextAsync(ServiceBusReceiver receiver)
{
    var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
    if (message is null)
    {
        return Results.NoContent();
    }

    await receiver.CompleteMessageAsync(message);
    return Results.Ok(message.Body.ToString());
}
