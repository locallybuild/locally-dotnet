using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

public class PostgreSQLTests
{
    // Provisioning in Locally is quick, but leave room for a cold CI runner.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task StoresAndReadsNotes()
    {
        using var cts = new CancellationTokenSource(Timeout);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Locally_Examples_Aspire_PostgreSQL_AppHost>(cts.Token);
        await using var app = await builder.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        using var http = app.CreateHttpClient("api");
        var id = Guid.NewGuid().ToString("N");

        var stored = await http.PutAsync($"/notes/{id}", new StringContent("hello from Locally"), cts.Token);
        Assert.Equal(HttpStatusCode.NoContent, stored.StatusCode);

        Assert.Equal("hello from Locally", await http.GetFromJsonAsync<string>($"/notes/{id}", cts.Token));
    }
}
