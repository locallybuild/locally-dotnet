using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

public class StorageTests
{
    // Provisioning in Locally is quick, but leave room for a cold CI runner.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task WritesAndReadsBlobsQueuesAndTables()
    {
        using var cts = new CancellationTokenSource(Timeout);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Locally_Examples_Aspire_Storage_AppHost>(cts.Token);
        await using var app = await builder.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        using var http = app.CreateHttpClient("api");
        var id = Guid.NewGuid().ToString("N");

        var created = await http.PostAsync($"/entries/{id}", new StringContent("hello from Locally"), cts.Token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var entry = await http.GetFromJsonAsync<Entry>($"/entries/{id}", cts.Token);
        Assert.Equal(new Entry(id, "hello from Locally", 18), entry);

        Assert.True(await ReceiveAsync(http, "/submissions/next", id, cts.Token), "the entry's ID wasn't received from the queue");
    }

    // Takes messages off until it finds the expected one, skipping any left behind by an earlier run.
    private static async Task<bool> ReceiveAsync(HttpClient http, string path, string expected, CancellationToken cancellationToken)
    {
        while (true)
        {
            var response = await http.PostAsync(path, null, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return false;
            }

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            if (await response.Content.ReadFromJsonAsync<string>(cancellationToken) == expected)
            {
                return true;
            }
        }
    }

    private record Entry(string Id, string Text, int Length);
}
