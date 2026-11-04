var builder = DistributedApplication.CreateBuilder(args);
builder.AddLocally();

var postgres = builder.AddAzurePostgresFlexibleServer("postgres");
var db = postgres.AddDatabase("db");

builder.AddProject<Projects.Locally_Examples_Aspire_PostgreSQL_Api>("api")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
