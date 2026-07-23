using System.Net;
using System.Text;

namespace Polishly.Platform.Tests;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = (req, ct) => Task.FromResult(handler(req));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request, cancellationToken);
    }

    public static HttpResponseMessage CreateSseResponse(HttpStatusCode statusCode, IEnumerable<string> sseLines)
    {
        var contentString = string.Join("\n", sseLines) + "\n";
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(contentString, Encoding.UTF8, "text/event-stream")
        };
    }

    public static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string jsonContent)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };
    }
}
