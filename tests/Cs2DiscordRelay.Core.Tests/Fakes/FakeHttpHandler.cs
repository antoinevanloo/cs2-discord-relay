using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Cs2DiscordRelay.Core.Tests.Fakes;

/// <summary>Replays scripted responses and records every request.</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpRequestMessage, HttpResponseMessage>> _responses;
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _fallback;

    public FakeHttpHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
    {
        _responses = new(responses);
        _fallback = _ => NoContent();
    }

    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    public IReadOnlyList<RecordedRequest> RequestList => [.. Requests];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Enqueue(new RecordedRequest(request.Method, request.RequestUri!, request.Content?.Headers.ContentType?.MediaType, body));
        var respond = _responses.TryDequeue(out var next) ? next : _fallback;
        return respond(request);
    }

    public static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

    public static HttpResponseMessage Status(HttpStatusCode status, string? json = null)
    {
        var response = new HttpResponseMessage(status);
        if (json is not null)
        {
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return response;
    }

    public static HttpResponseMessage WithHeaders(HttpResponseMessage response, params (string Name, string Value)[] headers)
    {
        foreach (var (name, value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }
}

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? MediaType, string Body);
