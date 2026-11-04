var builder = DistributedApplication.CreateBuilder(args);
builder.AddLocally();

var eventHubs = builder.AddAzureEventHubs("eventhubs");
var telemetry = eventHubs.AddHub("telemetry");

builder.AddProject<Projects.Locally_Examples_Aspire_EventHubs_Api>("api")
    .WithReference(telemetry)
    .WaitFor(telemetry);

builder.Build().Run();
