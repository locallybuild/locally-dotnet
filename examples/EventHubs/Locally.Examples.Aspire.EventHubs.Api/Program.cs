using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;

var builder = WebApplication.CreateBuilder(args);
builder.UseLocallyCredential();

builder.AddAzureEventHubProducerClient("telemetry");
builder.AddAzureEventHubConsumerClient("telemetry");

var app = builder.Build();

// Sends an event to the Event Hub.
app.MapPost("/events", async (HttpRequest request, EventHubProducerClient producer) =>
{
    using var reader = new StreamReader(request.Body);
    await producer.SendAsync([new EventData(await reader.ReadToEndAsync())]);
    return Results.Accepted();
});

// Reads every event in the Event Hub, from the earliest, until none arrive for a couple of seconds.
app.MapGet("/events", async (EventHubConsumerClient consumer, CancellationToken cancellationToken) =>
{
    var events = new List<string>();
    var options = new ReadEventOptions { MaximumWaitTime = TimeSpan.FromSeconds(2) };
    await foreach (var partitionEvent in consumer.ReadEventsAsync(startReadingAtEarliestEvent: true, options, cancellationToken))
    {
        if (partitionEvent.Data is null)
        {
            break;
        }

        events.Add(partitionEvent.Data.EventBody.ToString());
    }

    return events;
});

app.Run();
