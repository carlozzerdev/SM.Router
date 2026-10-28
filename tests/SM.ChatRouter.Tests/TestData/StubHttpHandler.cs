using System.Net;
using System.Text;

namespace SM.ChatRouter.Tests.TestData;

/// <summary>HttpMessageHandler falso: devuelve respuestas preparadas y guarda las peticiones recibidas.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<CapturedRequest> Requests { get; } = [];

    public StubHttpHandler Respond(HttpStatusCode status, string body, string contentType = "application/json", string? requestId = null)
    {
        _responses.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) };
            if (requestId is not null)
            {
                response.Headers.Add("x-request-id", requestId);
            }
            return response;
        });
        return this;
    }

    public StubHttpHandler RespondJson(string body) => Respond(HttpStatusCode.OK, body);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)), body));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No hay más respuestas preparadas en el stub.");
        }
        return _responses.Dequeue()(request);
    }

    public sealed record CapturedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string? Body);
}
