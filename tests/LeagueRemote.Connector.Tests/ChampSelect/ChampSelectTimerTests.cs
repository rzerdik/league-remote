using System.Text.Json;
using LeagueRemote.Connector.ChampSelect;
using LeagueRemote.Connector.State;

namespace LeagueRemote.Connector.Tests;

/// <summary>
/// The champ-select countdown. League's timer is a pair: <c>adjustedTimeLeftInPhase</c> is the time left at
/// <c>internalNowInEpochMs</c> (when the client took it from the server), and the session repeats that pair until the
/// next update; the client's own countdown subtracts the time since, and so must the phone's.
/// </summary>
public sealed class ChampSelectTimerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 20, 0, 0, TimeSpan.Zero);

    private static JsonElement Session(double timeLeftMs, double anchorEpochMs) =>
        TestLcu.Json(AutoPickSamples.CustomDraftBanTurn.Replace(
            "\"adjustedTimeLeftInPhase\": 30000, \"internalNowInEpochMs\": 0",
            $"\"adjustedTimeLeftInPhase\": {timeLeftMs}, \"internalNowInEpochMs\": {anchorEpochMs}"));

    private static double Ms(DateTimeOffset at) => at.ToUnixTimeMilliseconds();

    [Theory]
    [InlineData(30000, -5000, 25000)] // League took the timer 5 s ago: 5 s of it are gone
    [InlineData(30000, 0, 30000)] // just now
    [InlineData(30000, 2000, 30000)] // a clock that went back: never more than League's value
    [InlineData(4000, -6000, 0)] // already over
    public void Time_left_counts_from_leagues_timer_anchor(double timeLeftMs, double anchorOffsetMs, double expected)
    {
        Assert.Equal(expected, ChampSelectTimer.TimeLeftMs(timeLeftMs, Ms(Now) + anchorOffsetMs, Now));
    }

    [Fact]
    public void Time_left_without_an_anchor_is_leagues_value()
    {
        Assert.Equal(30000, ChampSelectTimer.TimeLeftMs(30000, 0, Now));
        Assert.Equal(0, ChampSelectTimer.TimeLeftMs(-1, 0, Now));
    }

    [Theory]
    [InlineData(-31000, true)] // older than the whole 30 s phase: a skewed clock, worth a log line
    [InlineData(-29000, false)]
    [InlineData(0, false)]
    public void Anchor_older_than_the_whole_phase_is_reported_as_stale(double anchorOffsetMs, bool stale)
    {
        var anchor = Ms(Now) + anchorOffsetMs;

        Assert.Equal(stale ? anchor : null, ChampSelectMapper.StaleTimerAnchor(Session(30000, anchor), Now));
    }

    [Fact]
    public void No_anchor_or_no_session_is_never_stale()
    {
        Assert.Null(ChampSelectMapper.StaleTimerAnchor(Session(30000, 0), Now));
        Assert.Null(ChampSelectMapper.StaleTimerAnchor(null, Now));
    }

    [Fact]
    public void Snapshot_carries_the_time_left_when_it_is_built()
    {
        var session = Session(30000, Ms(Now) - 5000);

        var state = GameStateMapper.Build(new LcuSnapshot("ChampSelect", ChampSelectSession: session), Now);

        Assert.Equal(25000, state.ChampSelect!.TimeLeftMs);
        Assert.Equal(Now, state.UpdatedAt);
    }

    [Fact]
    public void A_rebuild_of_an_unchanged_session_is_published_with_the_time_that_passed()
    {
        // The phone counts down from when a new value arrives. Rebuilding an unchanged session used to give the same
        // value (and the store dropped it as unchanged), so a phone that got a stale copy — the PC's cache replayed on
        // sign-in, a push opened a few seconds late — kept counting from that copy. Now RefreshState's rebuild corrects it.
        var session = Session(30000, Ms(Now) - 1000);
        var store = new StateStore();

        store.Set(GameStateMapper.Build(new LcuSnapshot("ChampSelect", ChampSelectSession: session), Now));
        var (first, v1) = store.Snapshot();
        store.Set(GameStateMapper.Build(new LcuSnapshot("ChampSelect", ChampSelectSession: session), Now.AddSeconds(4)));
        var (second, v2) = store.Snapshot();

        Assert.Equal((29000, 25000), (first.ChampSelect!.TimeLeftMs, second.ChampSelect!.TimeLeftMs));
        Assert.Equal(v1 + 1, v2);
    }
}
