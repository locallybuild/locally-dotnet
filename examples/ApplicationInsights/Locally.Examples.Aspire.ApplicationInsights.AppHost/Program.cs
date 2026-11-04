var builder = DistributedApplication.CreateBuilder(args);
builder.AddLocally();

var logs = builder.AddAzureLogAnalyticsWorkspace("logs");
var insights = builder.AddAzureApplicationInsights("insights")
    .WithLogAnalyticsWorkspace(logs);

builder.AddProject<Projects.Locally_Examples_Aspire_ApplicationInsights_Api>("api")
    .WithReference(insights)
    .WaitFor(insights);

builder.Build().Run();
