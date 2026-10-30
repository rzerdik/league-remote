using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using LeagueRemote.Connector.State;

namespace LeagueRemote.Connector.Tests;

/// <summary>
/// A <see cref="FakeLcu"/> in champ select that behaves like League for the automation: a hover PATCH sets the action's
/// champion, a PATCH with <c>completed: true</c> (or POST /complete) completes it unless <see cref="AcceptsLock"/> is off,
/// my-selection sets the local player's spells. GETs serve the session, the id lists, the champion summary and the
/// gameflow phase.
/// </summary>
internal sealed class DraftLcu(string session) : FakeLcu
{
    private const string ActionPrefix = "/lol-champ-select/v1/session/actions/";

    public JsonNode Session { get; set; } = JsonNode.Parse(session)!;

    public int[] Pickable { get; set; } = [7, 99, 103, 157, 238, 245, 81];

    public int[] Bannable { get; set; } = [7, 55, 81, 99, 103, 157, 238, 245, 777];

    public string RunePages { get; set; } = AutoPickSamples.RunePages;

    public bool SessionGone { get; set; }

    public bool AcceptsLock { get; set; } = true;

    public long LocalCell => Session["localPlayerCellId"]!.GetValue<long>();

    /// <summary>A state rebuild as <see cref="GameStateReader.ReadAsync"/> makes it, from the current data (no requests).</summary>
    public LcuSnapshot Snapshot(string phase) => new(
        phase,
        ChampSelectSession: phase == "ChampSelect" && !SessionGone ? TestLcu.Json(Session.ToJsonString()) : null,
        PickableChampionIds: Pickable,
        BannableChampionIds: Bannable,
        RunePages: TestLcu.Json(RunePages));

    public JsonObject Action(long id) =>
        Session["actions"]!.AsArray().SelectMany(g => g!.AsArray()).Select(a => a!.AsObject()).Single(a => a["id"]!.GetValue<long>() == id);

    public JsonObject Member(long cellId) =>
        Session["myTeam"]!.AsArray().Select(m => m!.AsObject()).Single(m => m["cellId"]!.GetValue<long>() == cellId);

    public void SetTimer(string phase, double timeLeftMs, bool infinite = false)
    {
        var timer = Session["timer"]!.AsObject();
        timer["phase"] = phase;
        timer["adjustedTimeLeftInPhase"] = timeLeftMs;
        timer["isInfinite"] = infinite;
    }

    protected override HttpResponseMessage Get(string path) => path switch
    {
        "/lol-champ-select/v1/session" when !SessionGone => Json(Session.ToJsonString()),
        "/lol-champ-select/v1/pickable-champion-ids" => Json(JsonSerializer.Serialize(Pickable)),
        "/lol-champ-select/v1/bannable-champion-ids" => Json(JsonSerializer.Serialize(Bannable)),
        "/lol-game-data/assets/v1/champion-summary.json" => Json(AutoPickSamples.ChampionSummary),
        "/lol-gameflow/v1/gameflow-phase" => Json(SessionGone ? "\"GameStart\"" : "\"ChampSelect\""),
        _ => base.Get(path),
    };

    protected override HttpResponseMessage Write(HttpMethod method, string path, string body)
    {
        if (path.StartsWith(ActionPrefix, StringComparison.Ordinal))
        {
            var rest = path[ActionPrefix.Length..];
            if (rest.EndsWith("/complete", StringComparison.Ordinal))
            {
                if (AcceptsLock)
                    Complete(Action(long.Parse(rest[..^"/complete".Length])));
            }
            else
            {
                var action = Action(long.Parse(rest));
                var patch = JsonNode.Parse(body)!;
                action["championId"] = patch["championId"]!.GetValue<int>();
                if (patch["completed"]?.GetValue<bool>() == true && AcceptsLock)
                    Complete(action);
            }
        }
        else if (path == "/lol-champ-select/v1/session/my-selection")
        {
            var patch = JsonNode.Parse(body)!;
            var me = Member(LocalCell);
            me["spell1Id"] = patch["spell1Id"]!.GetValue<int>();
            me["spell2Id"] = patch["spell2Id"]!.GetValue<int>();
        }

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private static void Complete(JsonObject action)
    {
        action["completed"] = true;
        action["isInProgress"] = false;
    }
}
