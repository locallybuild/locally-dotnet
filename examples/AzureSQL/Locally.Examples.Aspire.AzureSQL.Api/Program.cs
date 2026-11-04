using Azure.Core;
using Locally.Aspire.Client;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
builder.UseLocallyCredential();

// Aspire's connection string sets Authentication="Active Directory Default", which has SqlClient create a
// credential of its own - as such this removes it, and signs in using a LocallyCredential instead.
var credential = new LocallyCredential();
var connectionString = new SqlConnectionStringBuilder(builder.Configuration.GetConnectionString("db"));
connectionString.Remove("Authentication");

async Task<SqlConnection> OpenAsync()
{
    var connection = new SqlConnection(connectionString.ConnectionString)
    {
        AccessTokenCallback = async (context, cancellationToken) =>
        {
            var token = await credential.GetTokenAsync(
                new TokenRequestContext([context.Resource.TrimEnd('/') + "/.default"]), cancellationToken);
            return new SqlAuthenticationToken(token.Token, token.ExpiresOn);
        },
    };
    await connection.OpenAsync();
    return connection;
}

var app = builder.Build();

// Creates the notes table, if it doesn't already exist.
await using (var connection = await OpenAsync())
{
    await using var create = new SqlCommand(
        "IF OBJECT_ID('notes') IS NULL CREATE TABLE notes (id nvarchar(64) PRIMARY KEY, text nvarchar(max) NOT NULL)",
        connection);
    await create.ExecuteNonQueryAsync();
}

// Stores a note in the database.
app.MapPut("/notes/{id}", async (string id, HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    await using var connection = await OpenAsync();
    await using var upsert = new SqlCommand(
        "UPDATE notes SET text = @text WHERE id = @id; IF @@ROWCOUNT = 0 INSERT INTO notes (id, text) VALUES (@id, @text)",
        connection);
    upsert.Parameters.AddWithValue("@id", id);
    upsert.Parameters.AddWithValue("@text", await reader.ReadToEndAsync());
    await upsert.ExecuteNonQueryAsync();
    return Results.NoContent();
});

// Reads a note back from the database.
app.MapGet("/notes/{id}", async (string id) =>
{
    await using var connection = await OpenAsync();
    await using var select = new SqlCommand("SELECT text FROM notes WHERE id = @id", connection);
    select.Parameters.AddWithValue("@id", id);
    return await select.ExecuteScalarAsync() is string text ? Results.Ok(text) : Results.NotFound();
});

app.Run();
