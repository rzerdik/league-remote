using System.Net;

namespace LeagueRemote.Connector.Lcu;

/// <summary>
/// LCU returned a non-success status. <see cref="Exception.Message"/> carries League's own text for the PC's log; the phone
/// only ever gets a fixed sentence picked from it (<see cref="Commands.LcuRefusals"/>).
/// </summary>
internal sealed class LcuRequestException(HttpStatusCode statusCode, string? lcuMessage, string? errorCode = null)
    : Exception($"League client returned {(int)statusCode}{(string.IsNullOrWhiteSpace(lcuMessage) ? "" : $": {lcuMessage}")}")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>The <c>message</c> of League's error JSON; null when the body had none (or wasn't JSON).</summary>
    public string? LcuMessage { get; } = lcuMessage;

    /// <summary>The <c>errorCode</c> of League's error JSON (often just <c>RPC_ERROR</c>); null when there was none.</summary>
    public string? ErrorCode { get; } = errorCode;
}
