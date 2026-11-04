var builder = DistributedApplication.CreateBuilder(args);
builder.AddLocally();

var messaging = builder.AddAzureServiceBus("messaging");
messaging.AddServiceBusQueue("orders");
messaging.AddServiceBusTopic("events")
    .AddServiceBusSubscription("audit");

builder.AddProject<Projects.Locally_Examples_Aspire_ServiceBus_Api>("api")
    .WithReference(messaging)
    .WaitFor(messaging);

builder.Build().Run();
