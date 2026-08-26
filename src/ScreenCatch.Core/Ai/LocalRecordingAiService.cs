using System.Net.Http.Json;
using System.Text.Json;

namespace ScreenCatch.Core.Ai;

public sealed class LocalRecordingAiService : IRecordingAiService, IDisposable
{
    private readonly HttpMessageInvoker _client;

    public LocalRecordingAiService()
        : this(CreateDefaultHandler())
    {
    }

    internal static HttpClientHandler CreateDefaultHandler() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
    };

    public LocalRecordingAiService(HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _client = new HttpMessageInvoker(handler, disposeHandler: true);
    }

    public async Task<bool> ProbeAsync(
        RecordingAiOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled || !TryGetLocalEndpoint(options.Endpoint, out var endpoint))
        {
            return false;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(NormalizeTimeout(options.ProbeTimeout, TimeSpan.FromSeconds(2)));
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildEndpointUri(endpoint, "models"));
            using var response = await _client.SendAsync(request, timeout.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public async Task<RecordingAiSuggestion> SuggestAsync(
        RecordingAiRequest request,
        RecordingAiOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        var fallback = CreateFallback(request);
        if (!options.Enabled
            || string.IsNullOrWhiteSpace(options.Model)
            || !TryGetLocalEndpoint(options.Endpoint, out var endpoint)
            || !await ProbeAsync(options, cancellationToken).ConfigureAwait(false))
        {
            return fallback;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(NormalizeTimeout(options.RequestTimeout, TimeSpan.FromSeconds(15)));
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                BuildEndpointUri(endpoint, "chat/completions"));
            httpRequest.Content = JsonContent.Create(CreateChatPayload(request, options.Model));
            using var response = await _client.SendAsync(httpRequest, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return fallback;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var responseJson = await JsonDocument.ParseAsync(
                responseStream,
                cancellationToken: timeout.Token).ConfigureAwait(false);
            if (!TryGetResponseContent(responseJson.RootElement, out var content))
            {
                return fallback;
            }

            return TryParseSuggestion(content, out var suggestion) ? suggestion : fallback;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return fallback;
        }
        catch (HttpRequestException)
        {
            return fallback;
        }
        catch (IOException)
        {
            return fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
        catch (InvalidOperationException)
        {
            return fallback;
        }
    }

    public void Dispose() => _client.Dispose();

    private static object CreateChatPayload(RecordingAiRequest request, string model)
    {
        var metadata = $"Recorded at: {request.RecordedAtUtc.UtcDateTime:O}; "
            + $"duration seconds: {request.Duration.TotalSeconds:0.###}; "
            + $"source: {request.Source.ToString().ToLowerInvariant()}; "
            + $"preset: {request.PresetName ?? "none"}.";
        object userContent = request.SampleFramePng is { Length: > 0 } frame
            ? new object[]
            {
                new { type = "text", text = metadata },
                new
                {
                    type = "image_url",
                    image_url = new { url = $"data:image/png;base64,{Convert.ToBase64String(frame)}" },
                },
            }
            : metadata;
        return new
        {
            model,
            temperature = 0.2,
            max_tokens = 160,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "Suggest a concise title and one-sentence caption for this screen recording. "
                        + "Return only JSON with string properties title and caption.",
                },
                new { role = "user", content = userContent },
            },
        };
    }

    private static bool TryGetResponseContent(JsonElement root, out string? content)
    {
        content = null;
        if (!root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("message", out var message)
            || !message.TryGetProperty("content", out var contentProperty)
            || contentProperty.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        content = contentProperty.GetString();
        return true;
    }

    private static bool TryParseSuggestion(string? content, out RecordingAiSuggestion suggestion)
    {
        suggestion = null!;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return false;
        }

        using var document = JsonDocument.Parse(content[start..(end + 1)]);
        if (!document.RootElement.TryGetProperty("title", out var titleProperty)
            || !document.RootElement.TryGetProperty("caption", out var captionProperty))
        {
            return false;
        }

        var title = titleProperty.GetString()?.Trim();
        var caption = captionProperty.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(caption))
        {
            return false;
        }

        suggestion = new RecordingAiSuggestion(title, caption, IsFallback: false);
        return true;
    }

    private static TimeSpan NormalizeTimeout(TimeSpan? configured, TimeSpan fallback) =>
        configured is { } value && value > TimeSpan.Zero ? value : fallback;

    private static bool TryGetLocalEndpoint(string value, out Uri endpoint)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
            && parsed.IsLoopback
            && string.IsNullOrEmpty(parsed.UserInfo)
            && string.IsNullOrEmpty(parsed.Query)
            && string.IsNullOrEmpty(parsed.Fragment))
        {
            endpoint = parsed;
            return true;
        }

        endpoint = null!;
        return false;
    }

    private static Uri BuildEndpointUri(Uri endpoint, string relativePath)
    {
        var baseValue = endpoint.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? endpoint.AbsoluteUri
            : $"{endpoint.AbsoluteUri}/";
        return new Uri(new Uri(baseValue, UriKind.Absolute), relativePath);
    }

    private static RecordingAiSuggestion CreateFallback(RecordingAiRequest request)
    {
        var prefix = string.IsNullOrWhiteSpace(request.PresetName) ? "capture" : Slugify(request.PresetName);
        var title = $"{prefix}-{request.RecordedAtUtc.UtcDateTime:yyyyMMdd-HHmmss}";
        var caption = $"Screen recording captured {request.RecordedAtUtc.UtcDateTime:yyyy-MM-dd HH:mm} UTC.";
        return new RecordingAiSuggestion(title, caption, IsFallback: true);
    }

    private static string Slugify(string value)
    {
        var slug = new string(value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());
        return string.Join('-', slug.Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
