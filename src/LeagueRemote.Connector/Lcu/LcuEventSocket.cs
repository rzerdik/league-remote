using System.Net.WebSockets;
using System.Text.Json;

namespace LeagueRemote.Connector.Lcu;

/// <summary>
/// WAMP 1.0 subscription to the LCU event stream (<see cref="LcuTopics.All"/>). We only care *that* something changed;
/// the payload is ignored and the state is re-fetched (debounced) by the caller.
/// </summary>
internal sealed class LcuEventSocket(ILogger<LcuEventSocket> logger)
{
    private const int WampSubscribe = 5;
    private const int WampEvent = 8;

    /// <summary>Runs until the socket closes (e.g. client exit) or <paramref name="ct"/> is cancelled.</summary>
    public async Task RunAsync(LcuCredentials credentials, Action<string> onEvent, CancellationToken ct)
    {
        var uri = credentials.WebSocketUri;
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", $"Basic {credentials.BasicAuthParameter}");
        socket.Options.RemoteCertificateValidationCallback = (_, _, _, errors) => LcuHttp.IsTrustedCertificate(uri, errors);

        await socket.ConnectAsync(uri, ct);
        try
        {
            foreach (var topic in LcuTopics.All)
            {
                var subscribe = JsonSerializer.SerializeToUtf8Bytes(new object[] { WampSubscribe, topic });
                await socket.SendAsync(subscribe, WebSocketMessageType.Text, endOfMessage: true, ct);
            }

            logger.LogInformation("Subscribed to {Count} LCU event topics", LcuTopics.All.Count);

            var buffer = new byte[16 * 1024];
            using var message = new MemoryStream();
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                    continue;

                if (TryGetEventTopic(message.GetBuffer().AsSpan(0, (int)message.Length)) is { } topic)
                    onEvent(topic);
                message.SetLength(0);
            }
        }
        finally
        {
            await CloseAsync(socket);
        }
    }

    /// <summary>Says goodbye when we are the ones leaving (cancel, reconnect); a socket the client closed is left alone.</summary>
    private static async Task CloseAsync(ClientWebSocket socket)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
            return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            // The client is gone or slow; disposing the socket aborts it either way.
        }
    }

    /// <summary>Returns the topic of a <c>[8, topic, payload]</c> message, otherwise null.</summary>
    internal static string? TryGetEventTopic(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty)
            return null;

        try
        {
            var reader = new Utf8JsonReader(utf8);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray) return null;
            if (!reader.Read() || reader.TokenType != JsonTokenType.Number || reader.GetInt32() != WampEvent) return null;
            if (!reader.Read() || reader.TokenType != JsonTokenType.String) return null;
            return reader.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
