using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using TourGuide.API.Infrastructure.Options;
using TourGuide.API.Services.Implementations;
using Xunit;

namespace TourGuide.Backend.Tests;

public sealed class TranslationProviderTests
{
    [Fact]
    public async Task GoogleCloudTranslationProvider_WhenConfigured_ReturnsDecodedText()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":{"translations":[{"translatedText":"Hello &amp; welcome"}]}}""",
                    Encoding.UTF8,
                    "application/json"),
            });
        var provider = CreateProvider(handler);

        var result = await provider.TranslateAsync("Xin chào", "vi", "en");

        Assert.Equal("Hello & welcome", result.Text);
        Assert.Equal("GoogleCloud", result.Provider);
    }

    [Fact]
    public async Task GoogleCloudTranslationProvider_WhenApiFails_ThrowsProviderError()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"error":{"message":"invalid key"}}""",
                    Encoding.UTF8,
                    "application/json"),
            });
        var provider = CreateProvider(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranslateAsync("Xin chào", "vi", "en"));

        Assert.Contains("invalid key", ex.Message);
    }

    [Fact]
    public async Task MyMemoryTranslationProvider_WhenConfigured_ReturnsTranslatedText()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"responseData":{"translatedText":"Hello"},"responseStatus":200,"responseDetails":""}""",
                    Encoding.UTF8,
                    "application/json"),
            });
        var provider = new MyMemoryTranslationProvider(
            new HttpClient(handler),
            Options.Create(new TranslationProviderOptions
            {
                Enabled = true,
                ProviderName = "MyMemory",
                MyMemoryEndpoint = "https://api.mymemory.translated.net/get",
            }));

        var result = await provider.TranslateAsync("Xin chao", "vi", "en");

        Assert.Equal("Hello", result.Text);
        Assert.Equal("MyMemory", result.Provider);
    }

    [Fact]
    public async Task MyMemoryTranslationProvider_WhenApiFails_ThrowsProviderError()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"responseData":{"translatedText":null},"responseStatus":403,"responseDetails":"quota exceeded"}""",
                    Encoding.UTF8,
                    "application/json"),
            });
        var provider = new MyMemoryTranslationProvider(
            new HttpClient(handler),
            Options.Create(new TranslationProviderOptions
            {
                Enabled = true,
                ProviderName = "MyMemory",
                MyMemoryEndpoint = "https://api.mymemory.translated.net/get",
            }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranslateAsync("Xin chao", "vi", "en"));

        Assert.Contains("quota exceeded", ex.Message);
    }

    private static GoogleCloudTranslationProvider CreateProvider(HttpMessageHandler handler)
    {
        return new GoogleCloudTranslationProvider(
            new HttpClient(handler),
            Options.Create(new TranslationProviderOptions
            {
                Enabled = true,
                ProviderName = "GoogleCloud",
                ApiKey = "test-key",
                Endpoint = "https://translation.googleapis.com/language/translate/v2",
            }));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_send(request));
        }
    }
}
