using System.Net;
using LeagueRemote.Connector.AutoPick;
using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.State;
using LeagueRemote.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

/// <summary>Auto Accept: <see cref="AutoAcceptService"/> on a fake clock and a fake League client.</summary>
public sealed class AutoAcceptTests
{
    private const string ReadyCheckPath = "/lol-matchmaking/v1/ready-check";
    private const string Read = "GET " + ReadyCheckPath;
    private const string Accept = "POST " + ReadyCheckPath + "/accept";

    /// <summary>The service with its stores, in a temp settings folder (deleted on dispose); Auto Accept on unless asked otherwise.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _folder = Path.Combine(Path.GetTempPath(), $"lr-autoaccept-test-{Guid.NewGuid():N}");

        public Harness(bool autoAccept = true)
        {
            _http = TestLcu.LcuHttp(Lcu);
            Session = new FakeLcuSession(new LcuClient(_http));
            Settings = new AutoPickSettingsStore(_folder);
            Assert.True(Settings.Update(s => s with { AutoAccept = autoAccept }));
            var store = new AutoPickStore(Time);
            AutoPick = new AutoPickService(Settings, store, FakeLcuSession.InstantLock(Time), Time, NullLogger<AutoPickService>.Instance);
            Service = new AutoAcceptService(State, store, Settings, Session, Time, NullLogger<AutoAcceptService>.Instance);
        }

        public FakeTimeProvider Time { get; } = new(AutoPickHarness.Start);

        public FakeLcu Lcu { get; } = new();

        public FakeLcuSession Session { get; }

        public AutoPickSettingsStore Settings { get; }

        public AutoPickService AutoPick { get; }

        public StateStore State { get; } = new();

        public AutoAcceptService Service { get; }

        /// <summary>Publishes this ready check, without running a step; the client answers the same.</summary>
        public void Publish(string state = "InProgress", string response = "None")
        {
            Lcu.Routes[ReadyCheckPath] = ReadyCheckSamples.Check(state, response);
            State.Set(ClientState.Disconnected() with { ReadyCheck = new ReadyCheckState(state, response, 1) });
        }

        /// <summary>Publishes a ready check and runs a step, as the refresh loop and the service loop would.</summary>
        public Task ShowAsync(string state = "InProgress", string response = "None")
        {
            Publish(state, response);
            return Service.StepAsync(CancellationToken.None);
        }

        /// <summary>The check is over: the client answers "Invalid", which publishes no ready check.</summary>
        public Task ClearAsync()
        {
            Lcu.Routes[ReadyCheckPath] = ReadyCheckSamples.Check("Invalid");
            State.Set(ClientState.Disconnected());
            return Service.StepAsync(CancellationToken.None);
        }

        public Task AdvanceAsync(TimeSpan by)
        {
            Time.Advance(by);
            return Service.StepAsync(CancellationToken.None);
        }

        /// <summary>The wait runs out and the step acts on it.</summary>
        public Task WaitOutDelayAsync() => AdvanceAsync(AutoPickState.AutoAcceptDelay);

        public void Dispose()
        {
            _http.Dispose();
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task Accepts_after_the_delay_and_not_before()
    {
        using var h = new Harness();

        await h.ShowAsync();
        Assert.Empty(h.Lcu.Requests);

        await h.AdvanceAsync(AutoPickState.AutoAcceptDelay - TimeSpan.FromMilliseconds(1));
        Assert.Empty(h.Lcu.Requests);

        await h.AdvanceAsync(TimeSpan.FromMilliseconds(1));
        Assert.Equal([Read, Accept], h.Lcu.Requests);
        Assert.Equal(1, h.Session.Refreshes);
    }

    [Fact]
    public void The_delay_is_two_seconds() => Assert.Equal(TimeSpan.FromSeconds(2), AutoPickState.AutoAcceptDelay);

    [Fact]
    public async Task Does_nothing_when_off()
    {
        using var h = new Harness(autoAccept: false);

        await h.ShowAsync();
        await h.WaitOutDelayAsync();

        Assert.Empty(h.Lcu.Requests);
    }

    [Fact]
    public async Task A_ready_check_seen_while_off_stays_with_the_user_when_it_is_turned_on()
    {
        using var h = new Harness(autoAccept: false);
        await h.ShowAsync();

        Assert.Null(h.AutoPick.SetAutoAccept(true));
        await h.Service.StepAsync(CancellationToken.None);
        await h.WaitOutDelayAsync();

        Assert.Empty(h.Lcu.Requests);
    }

    [Fact]
    public async Task Works_with_auto_pick_off_and_paused()
    {
        using var h = new Harness();
        Assert.False(h.Settings.Current.Enabled);
        Assert.Null(h.AutoPick.Pause());

        await h.ShowAsync();
        await h.WaitOutDelayAsync();

        Assert.Equal([Read, Accept], h.Lcu.Requests);
    }

    [Theory]
    [InlineData("Accepted")]
    [InlineData("Declined")]
    public async Task An_answer_published_during_the_delay_cancels_it(string response)
    {
        using var h = new Harness();
        await h.ShowAsync();

        await h.ShowAsync(response: response);
        await h.WaitOutDelayAsync();

        Assert.Empty(h.Lcu.Requests);
    }

    [Theory]
    [InlineData("InProgress", "Accepted")]
    [InlineData("InProgress", "Declined")]
    [InlineData("StrangerNotReady", "None")]
    [InlineData("Invalid", "None")]
    public async Task The_re_read_decides_when_the_published_state_is_behind(string state, string response)
    {
        using var h = new Harness();
        await h.ShowAsync();

        h.Lcu.Routes[ReadyCheckPath] = ReadyCheckSamples.Check(state, response);
        await h.WaitOutDelayAsync();

        Assert.Equal([Read], h.Lcu.Requests);
    }

    [Fact]
    public async Task A_ready_check_that_ended_during_the_delay_is_not_accepted()
    {
        using var h = new Harness();
        await h.ShowAsync();

        await h.ShowAsync(state: "StrangerNotReady");
        await h.WaitOutDelayAsync();

        Assert.Empty(h.Lcu.Requests);
    }

    [Fact]
    public async Task Turning_it_off_during_the_delay_cancels_it_also_when_turned_on_again()
    {
        using var h = new Harness();
        await h.ShowAsync();

        Assert.Null(h.AutoPick.SetAutoAccept(false));
        await h.Service.StepAsync(CancellationToken.None);
        Assert.Null(h.AutoPick.SetAutoAccept(true));
        await h.Service.StepAsync(CancellationToken.None);
        await h.WaitOutDelayAsync();

        Assert.Empty(h.Lcu.Requests);
    }

    [Fact]
    public async Task Turned_off_right_before_the_delay_ends_it_does_not_even_read()
    {
        using var h = new Harness();
        await h.ShowAsync();

        Assert.Null(h.AutoPick.SetAutoAccept(false));
        await h.WaitOutDelayAsync();

        Assert.Empty(h.Lcu.Requests);
    }

    [Fact]
    public async Task Turned_off_while_the_re_read_is_on_its_way_it_does_not_accept()
    {
        using var h = new Harness();
        await h.ShowAsync();
        h.Lcu.Intercept = line =>
        {
            if (line == Read)
                Assert.True(h.Settings.Update(s => s with { AutoAccept = false }));
            return null;
        };

        await h.WaitOutDelayAsync();

        Assert.Equal([Read], h.Lcu.Requests);
    }

    [Fact]
    public async Task Tries_once_per_ready_check()
    {
        using var h = new Harness();
        await h.ShowAsync();
        await h.WaitOutDelayAsync();

        // The accept hasn't shown up in the state yet, and rebuilds keep coming.
        await h.ShowAsync();
        await h.WaitOutDelayAsync();
        await h.ShowAsync(response: "Accepted");
        await h.WaitOutDelayAsync();

        Assert.Equal([Read, Accept], h.Lcu.Requests);
    }

    [Fact]
    public async Task A_later_ready_check_is_accepted_again()
    {
        using var h = new Harness();
        await h.ShowAsync();
        await h.WaitOutDelayAsync();
        await h.ShowAsync(response: "Accepted");

        // Someone declined: back to searching, then a new match.
        await h.ShowAsync(state: "StrangerNotReady", response: "Accepted");
        await h.ClearAsync();
        await h.ShowAsync();
        Assert.Equal([Read, Accept], h.Lcu.Requests);

        await h.WaitOutDelayAsync();

        Assert.Equal([Read, Accept, Read, Accept], h.Lcu.Requests);
    }

    [Fact]
    public async Task A_failed_accept_is_survived_not_retried_and_the_next_ready_check_still_works()
    {
        using var h = new Harness();
        h.Lcu.Failures[Accept] = (HttpStatusCode.InternalServerError, "nope");

        await h.ShowAsync();
        await h.WaitOutDelayAsync();
        await h.WaitOutDelayAsync();
        Assert.Equal([Read, Accept], h.Lcu.Requests);
        Assert.Equal(0, h.Session.Refreshes);

        h.Lcu.Failures.Clear();
        await h.ClearAsync();
        await h.ShowAsync();
        await h.WaitOutDelayAsync();

        Assert.Equal([Read, Accept, Read, Accept], h.Lcu.Requests);
    }

    [Fact]
    public async Task A_failed_re_read_is_survived_and_nothing_is_accepted()
    {
        using var h = new Harness();
        await h.ShowAsync();
        h.Lcu.Failures[Read] = (HttpStatusCode.ServiceUnavailable, "busy");

        await h.WaitOutDelayAsync();

        Assert.Equal([Read], h.Lcu.Requests);
    }

    [Fact]
    public async Task Without_a_client_at_the_end_of_the_delay_nothing_happens()
    {
        using var h = new Harness();
        await h.ShowAsync();
        h.Session.Client = null;

        await h.WaitOutDelayAsync();

        Assert.Empty(h.Lcu.Requests);
    }

    [Fact]
    public async Task The_caller_stopping_is_not_treated_as_a_failure()
    {
        using var h = new Harness();
        await h.ShowAsync();
        h.Time.Advance(AutoPickState.AutoAcceptDelay);
        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Service.StepAsync(stopped.Token));

        Assert.DoesNotContain(Accept, h.Lcu.Requests);
    }

    // --- The loop -------------------------------------------------------------------------

    /// <summary>Waits (bounded) until the running loop has stepped past <paramref name="steps"/>.</summary>
    private static void WaitForStep(Harness h, int steps) =>
        Assert.True(SpinWait.SpinUntil(() => h.Service.Steps > steps, TimeSpan.FromSeconds(10)));

    [Fact]
    public async Task The_loop_wakes_for_a_published_ready_check_and_for_the_end_of_the_delay()
    {
        using var h = new Harness();
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Lcu.Intercept = line =>
        {
            if (line == Accept)
                accepted.TrySetResult();
            return null;
        };

        await h.Service.StartAsync(CancellationToken.None);
        try
        {
            WaitForStep(h, 0);
            var steps = h.Service.Steps;
            h.Publish();
            WaitForStep(h, steps);

            h.Time.Advance(AutoPickState.AutoAcceptDelay);
            await accepted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await h.Service.StopAsync(CancellationToken.None);
        }

        Assert.Equal([Read, Accept], h.Lcu.Requests);
    }

    [Fact]
    public async Task The_loop_wakes_when_the_setting_is_turned_off_and_stays_quiet_after()
    {
        using var h = new Harness();
        await h.Service.StartAsync(CancellationToken.None);
        try
        {
            WaitForStep(h, 0);
            var steps = h.Service.Steps;
            h.Publish();
            WaitForStep(h, steps);

            steps = h.Service.Steps;
            Assert.Null(h.AutoPick.SetAutoAccept(false));
            WaitForStep(h, steps);

            h.Time.Advance(AutoPickState.AutoAcceptDelay);
        }
        finally
        {
            await h.Service.StopAsync(CancellationToken.None);
        }

        Assert.Empty(h.Lcu.Requests);
    }
}
