var builder = DistributedApplication.CreateBuilder(args);
builder.AddLocally();

var storage = builder.AddAzureStorage("storage");
var entries = storage.AddBlobContainer("entries");
var submissions = storage.AddQueue("submissions");
var tables = storage.AddTables("tables");

builder.AddProject<Projects.Locally_Examples_Aspire_Storage_Api>("api")
    .WithReference(entries)
    .WithReference(submissions)
    .WithReference(tables)
    .WaitFor(storage);

builder.Build().Run();
