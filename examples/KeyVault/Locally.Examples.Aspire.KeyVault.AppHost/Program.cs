var builder = DistributedApplication.CreateBuilder(args);
builder.AddLocally();

var secrets = builder.AddAzureKeyVault("secrets");

builder.AddProject<Projects.Locally_Examples_Aspire_KeyVault_Api>("api")
    .WithReference(secrets)
    .WaitFor(secrets);

builder.Build().Run();
