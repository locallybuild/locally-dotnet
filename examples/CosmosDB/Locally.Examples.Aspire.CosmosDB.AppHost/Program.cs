var builder = DistributedApplication.CreateBuilder(args);
builder.AddLocally();

var cosmos = builder.AddAzureCosmosDB("cosmos");
cosmos.AddCosmosDatabase("app")
    .AddContainer("items", "/id");

builder.AddProject<Projects.Locally_Examples_Aspire_CosmosDB_Api>("api")
    .WithReference(cosmos)
    .WaitFor(cosmos);

builder.Build().Run();
