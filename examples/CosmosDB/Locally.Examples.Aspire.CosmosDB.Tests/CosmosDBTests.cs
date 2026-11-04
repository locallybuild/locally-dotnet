using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

public class CosmosDBTests
{
    // Provisioning in Locally is quick, but leave room for a cold CI runner.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task StoresAndReadsItems()
    {
        using var cts = new CancellationTokenSource(Timeout);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Locally_Examples_Aspire_CosmosDB_AppHost>(cts.Token);
        await using var app = await builder.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        using var http = app.CreateHttpClient("api");
        var id = Guid.NewGuid().ToString("N");

        var stored = await http.PutAsync($"/items/{id}", new StringContent("hello from Locally"), cts.Token);
        Assert.Equal(HttpStatusCode.NoContent, stored.StatusCode);

        var item = await http.GetFromJsonAsync<Item>($"/items/{id}", cts.Token);
        Assert.Equal(new Item(id, "hello from Locally"), item);
    }

    private record Item(string Id, string Text);
}
