using LeagueRemote.Connector.AutoPick;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

/// <summary>
/// An <see cref="AutoPickService"/> on a <see cref="FakeTimeProvider"/>, a settings store in its own temp folder (deleted on
/// dispose) and a <see cref="DraftLcu"/>. <see cref="ObserveAsync(string)"/> hands the service a state rebuild of the fake's
/// current session and runs one step, as the refresh loop and the service loop would.
/// </summary>
internal sealed class AutoPickHarness : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The hover of the draft ban turn: ban action 11, Zed.</summary>
    public const string Hover11Zed = "PATCH /lol-champ-select/v1/session/actions/11 {\"championId\":238}";

    public const string Summary = "GET /lol-game-data/assets/v1/champion-summary.json";

    public const string GetSession = "GET /lol-champ-select/v1/session";

    private readonly HttpClient _http;

    public AutoPickHarness(AutoPickSettings? settings = null, string session = AutoPickSamples.DraftBanTurn)
    {
        Lcu = new DraftLcu(session);
        _http = TestLcu.LcuHttp(Lcu);
        Client = new LcuClient(_http);
        Settings = new AutoPickSettingsStore(Folder);
        if (settings is not null)
            Assert.True(Settings.Update(_ => settings));
        Store = new AutoPickStore(Time);
        Service = new AutoPickService(Settings, Store, FakeLcuSession.InstantLock(Time), Time, NullLogger<AutoPickService>.Instance);
    }

    public FakeTimeProvider Time { get; } = new(Start);

    public string Folder { get; } = Path.Combine(Path.GetTempPath(), $"lr-autopick-test-{Guid.NewGuid():N}");

    public DraftLcu Lcu { get; }

    public LcuClient Client { get; }

    public AutoPickSettingsStore Settings { get; }

    public AutoPickStore Store { get; }

    public AutoPickService Service { get; }

    public AutoPickState State => Store.Snapshot().Value;

    public AutoPickRun Run => State.Run ?? throw new InvalidOperationException("No run.");

    /// <summary>Every request but the GETs.</summary>
    public IReadOnlyList<string> Writes => [.. Lcu.Requests.Where(r => !r.StartsWith("GET ", StringComparison.Ordinal))];

    /// <summary>Draft presets most tests use: bans Zed then Yasuo, picks Ahri, Lux, Ekko; no spells, no rune page.</summary>
    public static AutoPickSettings Draft(bool enabled = true, params AutoPickPreset[] more) =>
        new(enabled, [new(AutoPickQueueGroups.Draft, AutoPickRoles.Any, [238, 157], [103, 99, 245]), .. more]);

    public Task ObserveAsync(string phase = "ChampSelect") => ObserveAsync(Lcu.Snapshot(phase));

    /// <summary>Hands the service a rebuild taken earlier (the LCU may have moved on since) and runs a step.</summary>
    public async Task ObserveAsync(LcuSnapshot snapshot)
    {
        Service.Observe(Client, snapshot);
        await Service.StepAsync(CancellationToken.None);
    }

    /// <summary>Lets time pass and runs a step on the last rebuild (the loop waking for a due action).</summary>
    public async Task AdvanceAsync(double seconds)
    {
        Time.Advance(TimeSpan.FromSeconds(seconds));
        await Service.StepAsync(CancellationToken.None);
    }

    /// <summary>Completes when the store holds a state matching <paramref name="match"/> (now or after a change).</summary>
    public Task WhenState(Func<AutoPickState, bool> match)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check()
        {
            if (match(Store.Snapshot().Value))
                tcs.TrySetResult();
        }

        Store.Changed += Check;
        Check();
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(10)).ContinueWith(t =>
        {
            Store.Changed -= Check;
            return t;
        }, TaskScheduler.Default).Unwrap();
    }

    public void Dispose()
    {
        _http.Dispose();
        if (Directory.Exists(Folder))
            Directory.Delete(Folder, recursive: true);
    }
}
