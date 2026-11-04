var builder = DistributedApplication.CreateBuilder(args);
builder.AddLocally();

var cache = builder.AddAzureManagedRedis("cache");

builder.AddProject<Projects.Locally_Examples_Aspire_Redis_Api>("api")
    .WithReference(cache)
    .WaitFor(cache);

builder.Build().Run();
