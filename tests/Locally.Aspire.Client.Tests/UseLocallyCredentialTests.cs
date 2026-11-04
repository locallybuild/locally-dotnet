using System.Net;
using System.Text;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Locally.Aspire.Client.Tests;

public class UseLocallyCredentialTests
{
    private const string BlobEndpoint = "https://account.blob.core.storage.locally:5660/";

    [Fact]
    public async Task AspireAzureClientsSignInWithIt()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new());
        var requests = new RecordingHandler();
        builder.UseLocallyCredential(new FakeCredential("locally-token"));
        builder.AddAzureBlobServiceClient("blobs",
            settings => settings.ServiceUri = new Uri(BlobEndpoint),
            clients => clients.ConfigureOptions(options => options.Transport = new HttpClientTransport(requests)));

        using var host = builder.Build();
        await Send(host.Services.GetRequiredService<BlobServiceClient>());

        Assert.Equal("Bearer locally-token", requests.Authorization);
    }

    [Fact]
    public async Task AClientsOwnCredentialWins()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new());
        var requests = new RecordingHandler();
        builder.UseLocallyCredential(new FakeCredential("locally-token"));
        builder.AddAzureBlobServiceClient("blobs",
            settings =>
            {
                settings.ServiceUri = new Uri(BlobEndpoint);
                settings.Credential = new FakeCredential("azure-token");
            },
            clients => clients.ConfigureOptions(options => options.Transport = new HttpClientTransport(requests)));

        using var host = builder.Build();
        await Send(host.Services.GetRequiredService<BlobServiceClient>());

        Assert.Equal("Bearer azure-token", requests.Authorization);
    }

    [Theory]
    [InlineData("cache.redis.locally:61241,ssl=true", true)]
    [InlineData("cache.redis.locally:61241,ssl=true,password=secret", false)]
    [InlineData("cache.redis.locally:61241", false)]
    [InlineData("cache.redis.cache.windows.net:6380,ssl=true", false)]
    [InlineData("localhost:6379", false)]
    public void RedisSignsInToLocallysCachesWithIt(string connectionString, bool signsIn)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:cache"] = connectionString,
        });
        builder.UseLocallyCredential(new FakeCredential(Jwt()));
        builder.AddRedisClient("cache");

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<ConfigurationOptions>>().Value;

        Assert.Equal(signsIn, options.Password == Jwt());
    }

    // Sends one request; only what it was sent with matters.
    private static async Task Send(BlobServiceClient client)
    {
        try
        {
            await client.GetBlobContainerClient("entries").GetPropertiesAsync();
        }
        catch (Azure.RequestFailedException)
        {
        }
    }

    // A token the Redis client can read an object ID from.
    private static string Jwt()
    {
        static string Encode(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Encode("""{"alg":"none"}""")}.{Encode("""{"oid":"b00278ff-316d-4c68-878e-890b6c0449c2","tid":"59e01520-e049-4dfa-8a7d-1a847fc07763","exp":4102444800}""")}.c2ln";
    }

    private sealed class FakeCredential(string token) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new(token, DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new(GetToken(requestContext, cancellationToken));
    }

    // Answers every request with a 404, and keeps the Authorization it was sent.
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
