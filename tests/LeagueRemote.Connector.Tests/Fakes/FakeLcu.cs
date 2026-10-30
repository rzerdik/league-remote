using System.Net;
using System.Text;
using System.Text.Json;

namespace LeagueRemote.Connector.Tests;

/// <summary>
/// The fake League client. GETs answer from <see cref="Routes"/> by path (404 otherwise; the query string doesn't take
/// part), writes answer 204, and a request listed in <see cref="Failures"/> gets that LCU error instead. Every request is
/// recorded as <c>"METHOD /path?query body"</c>. <see cref="Intercept"/> sees each request first, for stateful cases;
/// a fake with behaviour of its own overrides <see cref="Get"/> and <see cref="Write"/>.
/// </summary>
internal class FakeLcu : HttpMessageHandler
{
    /// <summary>GET answers (JSON) by path.</summary>
    public Dictionary<string, string> Routes { get; } = [];

    /// <summary>Requests (<c>"METHOD /path"</c>) that fail with this status and LCU error message.</summary>
    public Dictionary<string, (HttpStatusCode Status, string Message)> Failures { get; } = [];

    /// <summary>Every request so far, in order.</summary>
    public List<string> Requests { get; } = [];

    /// <summary>
    /// Called with each request line once it is recorded: returns the answer, or null to answer as usual (it may change
    /// <see cref="Routes"/> first, e.g. to show a lock that a write just made).
    /// </summary>
    public Func<string, HttpResponseMessage?>? Intercept { get; set; }

    public HttpClient CreateClient() => TestLcu.LcuHttp(this);

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>An error in the shape League sends: <c>{ errorCode, httpStatus, message }</c>.</summary>
    public static HttpResponseMessage Error(HttpStatusCode status, string message) =>
        Json(JsonSerializer.Serialize(new { errorCode = "RPC_ERROR", httpStatus = (int)status, message }), status);

    protected sealed override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        var path = request.RequestUri!.AbsolutePath;
        var line = $"{request.Method} {path}{request.RequestUri.Query}{(body.Length > 0 ? " " + body : "")}";
        Requests.Add(line);

        if (Intercept?.Invoke(line) is { } answer)
            return answer;
        if (Failures.TryGetValue($"{request.Method} {path}", out var failure))
            return Error(failure.Status, failure.Message);
        return request.Method == HttpMethod.Get ? Get(path) : Write(request.Method, path, body);
    }

    protected virtual HttpResponseMessage Get(string path) =>
        Routes.TryGetValue(path, out var json) ? Json(json) : Error(HttpStatusCode.NotFound, "not found");

    protected virtual HttpResponseMessage Write(HttpMethod method, string path, string body) => new(HttpStatusCode.NoContent);
}
