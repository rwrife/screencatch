using System.Net;
using System.Text;
using System.Text.Json;
using ScreenCatch.Core.Ai;
using ScreenCatch.Core.Capture;

namespace ScreenCatch.Core.Tests;

public sealed class LocalRecordingAiServiceTests
{
    [Fact]
    public void CreateDefaultHandler_DisablesRedirectsAndSystemProxies()
    {
        using var handler = LocalRecordingAiService.CreateDefaultHandler();

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
    }

    [Fact]
    public async Task SuggestAsync_WhenDisabled_ReturnsFallbackWithoutNetworkRequest()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Network must not be used."));
        using var service = new LocalRecordingAiService(handler);
        var request = CreateRequest();

        var result = await service.SuggestAsync(request, new RecordingAiOptions());

        Assert.True(result.IsFallback);
        Assert.Equal("issue-repro-20260826-143000", result.Title);
        Assert.Contains("2026-08-26 14:30 UTC", result.Caption);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SuggestAsync_WhenEndpointIsNotLoopback_ReturnsFallbackWithoutNetworkRequest()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Network must not be used."));
        using var service = new LocalRecordingAiService(handler);
        var options = new RecordingAiOptions(Enabled: true, Endpoint: "https://api.openai.com/v1/");

        var reachable = await service.ProbeAsync(options);
        var result = await service.SuggestAsync(CreateRequest(), options);

        Assert.False(reachable);
        Assert.True(result.IsFallback);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ProbeAsync_WhenEnabledAndLocal_RequestsOnlyTheModelsEndpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var service = new LocalRecordingAiService(handler);
        var options = new RecordingAiOptions(Enabled: true, Endpoint: "http://127.0.0.1:11434/v1");

        var reachable = await service.ProbeAsync(options);

        Assert.True(reachable);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://127.0.0.1:11434/v1/models", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task SuggestAsync_WhenLocalModelResponds_SendsMinimalMetadataAndReturnsSuggestion()
    {
        string? chatBody = null;
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            chatBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse("""
                {"choices":[{"message":{"content":"{\"title\":\"Region workflow demo\",\"caption\":\"A short region capture showing the issue workflow.\"}"}}]}
                """);
        });
        using var service = new LocalRecordingAiService(handler);
        var options = new RecordingAiOptions(Enabled: true, Endpoint: "http://localhost:11434/v1/", Model: "qwen2.5:3b");

        var result = await service.SuggestAsync(CreateRequest(), options);

        Assert.False(result.IsFallback);
        Assert.Equal("Region workflow demo", result.Title);
        Assert.Equal("A short region capture showing the issue workflow.", result.Caption);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("http://localhost:11434/v1/chat/completions", handler.Requests[1].RequestUri!.AbsoluteUri);
        using var payload = JsonDocument.Parse(chatBody!);
        Assert.Equal("qwen2.5:3b", payload.RootElement.GetProperty("model").GetString());
        var serialized = chatBody!;
        Assert.Contains("region", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("12.5", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("image_url", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuggestAsync_WhenModelResponseIsMalformed_ReturnsFallbackWithoutSurfacingError()
    {
        var handler = new RecordingHandler(request => request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.OK)
            : JsonResponse("{}"));
        using var service = new LocalRecordingAiService(handler);
        var options = new RecordingAiOptions(Enabled: true);

        var result = await service.SuggestAsync(CreateRequest(), options);

        Assert.True(result.IsFallback);
        Assert.Equal("issue-repro-20260826-143000", result.Title);
    }

    [Fact]
    public async Task SuggestAsync_WhenResponseStreamFails_ReturnsFallback()
    {
        var handler = new RecordingHandler(request => request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.OK)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ThrowingContent() });
        using var service = new LocalRecordingAiService(handler);

        var result = await service.SuggestAsync(CreateRequest(), new RecordingAiOptions(Enabled: true));

        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task ProbeAsync_WhenEndpointDoesNotRespond_TimesOutGracefully()
    {
        using var service = new LocalRecordingAiService(new DelayedHandler());
        var options = new RecordingAiOptions(
            Enabled: true,
            ProbeTimeout: TimeSpan.FromMilliseconds(10));

        var reachable = await service.ProbeAsync(options);

        Assert.False(reachable);
    }

    [Fact]
    public async Task SuggestAsync_WhenGenerationDoesNotRespond_TimesOutToFallback()
    {
        using var service = new LocalRecordingAiService(new ProbeThenDelayHandler());
        var options = new RecordingAiOptions(
            Enabled: true,
            RequestTimeout: TimeSpan.FromMilliseconds(10));

        var result = await service.SuggestAsync(CreateRequest(), options);

        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task SuggestAsync_WhenEndpointIsUnavailable_ReturnsFallbackAfterProbe()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var service = new LocalRecordingAiService(handler);

        var result = await service.SuggestAsync(CreateRequest(), new RecordingAiOptions(Enabled: true));

        Assert.True(result.IsFallback);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
    }

    [Fact]
    public async Task SuggestAsync_WithSampleFrame_SendsExactlyOneInlinePng()
    {
        string? chatBody = null;
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            chatBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse("""
                {"choices":[{"message":{"content":"{\"title\":\"Frame demo\",\"caption\":\"A caption.\"}"}}]}
                """);
        });
        using var service = new LocalRecordingAiService(handler);

        var result = await service.SuggestAsync(
            CreateRequest([1, 2, 3]),
            new RecordingAiOptions(Enabled: true));

        Assert.False(result.IsFallback);
        Assert.Equal(1, CountOccurrences(chatBody!, "data:image/png;base64,"));
        Assert.Contains("AQID", chatBody!, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string token) =>
        value.Split(token, StringSplitOptions.None).Length - 1;

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static RecordingAiRequest CreateRequest(byte[]? sampleFramePng = null) => new(
        RecordedAtUtc: new DateTimeOffset(2026, 8, 26, 14, 30, 0, TimeSpan.Zero),
        Duration: TimeSpan.FromSeconds(12.5),
        Source: CaptureSourceKind.Region,
        PresetName: "issue-repro",
        SampleFramePng: sampleFramePng);

    private sealed class ThrowingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.FromException(new IOException("Local endpoint disconnected."));

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new ThrowingReadStream());

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class ThrowingReadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new IOException("Local endpoint disconnected.");

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Local endpoint disconnected."));
    }

    private sealed class ProbeThenDelayHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }
}
