using System.Net;
using LeagueRemote.Connector.Lcu;
using Microsoft.Extensions.Logging;

namespace LeagueRemote.Connector.Tests;

public sealed class LcuFailureLogTests
{
    [Fact]
    public void League_s_own_text_goes_to_debug_and_the_warning_keeps_status_and_error_code()
    {
        var logger = new ListLogger();

        logger.LogLcuFailure("State refresh", new LcuRequestException(HttpStatusCode.BadRequest, "Summoner Friend#EUW is busy", "RPC_ERROR"));

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Equal("State refresh failed: HTTP 400 RPC_ERROR", warning.Message);
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Information && e.Message.Contains("Friend"));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("Summoner Friend#EUW is busy"));
    }

    [Fact]
    public void A_refusal_without_error_code_or_text_logs_no_placeholder()
    {
        var logger = new ListLogger();

        logger.LogLcuFailure("Friends refresh", new LcuRequestException(HttpStatusCode.NotFound, null));

        Assert.Equal("Friends refresh failed: HTTP 404 ", Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning).Message);
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("(null)"));
    }

    [Fact]
    public void Other_failures_keep_their_message_at_warning()
    {
        var logger = new ListLogger();

        logger.LogLcuFailure("Chat refresh", new HttpRequestException("Connection refused"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal((LogLevel.Warning, "Chat refresh failed: Connection refused"), (entry.Level, entry.Message));
    }

    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
