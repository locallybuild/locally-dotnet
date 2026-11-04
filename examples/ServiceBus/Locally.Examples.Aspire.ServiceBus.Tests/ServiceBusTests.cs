using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

public class ServiceBusTests
{
    // Provisioning in Locally is quick, but leave room for a cold CI runner.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task SendsAndReceivesThroughQueuesAndTopics()
    {
        using var cts = new CancellationTokenSource(Timeout);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Locally_Examples_Aspire_ServiceBus_AppHost>(cts.Token);
        await using var app = await builder.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        using var http = app.CreateHttpClient("api");
        var id = Guid.NewGuid().ToString("N");

        Assert.Equal(HttpStatusCode.Accepted, (await http.PostAsync($"/orders/{id}", null, cts.Token)).StatusCode);
        Assert.True(await ReceiveAsync(http, "/orders/next", id, cts.Token), "the order wasn't received from the queue");

        Assert.Equal(HttpStatusCode.Accepted, (await http.PostAsync($"/events/{id}", null, cts.Token)).StatusCode);
        Assert.True(await ReceiveAsync(http, "/events/audit/next", id, cts.Token), "the event wasn't received from the subscription");
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
}
