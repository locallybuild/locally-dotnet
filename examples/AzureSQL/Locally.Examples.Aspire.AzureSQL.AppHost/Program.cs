var builder = DistributedApplication.CreateBuilder(args);
builder.AddLocally();

var sql = builder.AddAzureSqlServer("sql");
var db = sql.AddDatabase("db");

builder.AddProject<Projects.Locally_Examples_Aspire_AzureSQL_Api>("api")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
