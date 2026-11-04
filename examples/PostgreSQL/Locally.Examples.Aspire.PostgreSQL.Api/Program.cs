using Locally.Aspire.Client;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.UseLocallyCredential();

// Aspire's PostgreSQL data source creates a credential of its own, so it needs to be given a LocallyCredential.
builder.AddAzureNpgsqlDataSource("db", settings => settings.Credential = new LocallyCredential());

var app = builder.Build();

// Creates the notes table, if it doesn't already exist.
await using (var create = app.Services.GetRequiredService<NpgsqlDataSource>().CreateCommand(
    "CREATE TABLE IF NOT EXISTS notes (id text PRIMARY KEY, text text NOT NULL)"))
{
    await create.ExecuteNonQueryAsync();
}

// Stores a note in the database.
app.MapPut("/notes/{id}", async (string id, HttpRequest request, NpgsqlDataSource db) =>
{
    using var reader = new StreamReader(request.Body);
    await using var upsert = db.CreateCommand(
        "INSERT INTO notes (id, text) VALUES ($1, $2) ON CONFLICT (id) DO UPDATE SET text = EXCLUDED.text");
    upsert.Parameters.AddWithValue(id);
    upsert.Parameters.AddWithValue(await reader.ReadToEndAsync());
    await upsert.ExecuteNonQueryAsync();
    return Results.NoContent();
});

// Reads a note back from the database.
app.MapGet("/notes/{id}", async (string id, NpgsqlDataSource db) =>
{
    await using var select = db.CreateCommand("SELECT text FROM notes WHERE id = $1");
    select.Parameters.AddWithValue(id);
    return await select.ExecuteScalarAsync() is string text ? Results.Ok(text) : Results.NotFound();
});

app.Run();
