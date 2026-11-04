var builder = DistributedApplication.CreateBuilder(args);
builder.AddLocally();

var config = builder.AddAzureAppConfiguration("config");

builder.AddProject<Projects.Locally_Examples_Aspire_AppConfiguration_Api>("api")
    .WithReference(config)
    .WaitFor(config);

builder.Build().Run();
