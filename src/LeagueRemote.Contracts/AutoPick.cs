using System.Text.Json.Serialization;

namespace LeagueRemote.Contracts;

/// <summary>
/// Automatic ban, pick, summoner spells and rune page in champ select, run by the connector (it works while the phone
/// sleeps). Published as its own hub message (<see cref="HubMethods.Client.AutoPickUpdated"/>), replayed to phones that
/// connect later and never pushed. Its rhythm is its own: the presets change when the phone edits them, the run a few times
/// per champ select, so it doesn't ride <see cref="ClientState"/> (which churns with every hover and would carry the
/// presets in every update). Never contains a player's name or id: champions, spells and the user's own rune pages only.
/// <para>
/// The rules the connector follows (the spec; see also <c>docs/auto-pick.md</c>):
/// </para>
/// <list type="number">
/// <item><b>No writes unless</b> <see cref="AutoPickSettings.Enabled"/>, not <see cref="PausedForThisGame"/>, the champ-select
/// session's <c>queueId</c> maps to a group (<see cref="AutoPickQueueGroups.ForQueue"/>, which lists the Summoner's Rift
/// Blind / Draft / Tournament Draft custom queues too; a custom game without a queue id — a legacy custom lobby — isn't
/// automated, the session can't tell its map or Practice Tool apart) and the resolved preset (<see cref="AutoPickSettings.Resolve"/>) has something for the step.
/// Disabled, paused or unsupported: not a single LCU write, not even spells.</item>
/// <item><b>Role</b>: the local player's <c>assignedPosition</c> in <c>myTeam</c> (lowercase in the LCU, upper-cased to a
/// <see cref="Positions"/> value); "" (blind pick, ARAM) → <see cref="AutoPickRoles.Any"/>. Read again when each step starts,
/// so a position swap is followed.</item>
/// <item><b>Ban / pick</b>: only when the local player's ban or pick action has <c>isInProgress</c> and is not completed,
/// with the timer phase <c>BAN_PICK</c> (League marks the first ban in progress already during <c>PLANNING</c>; ignore that).
/// Not with <c>benchEnabled</c> (ARAM-style) sessions or <see cref="AutoPickQueueGroups.Aram"/>. Each action is handled at
/// most once (by its id).
///   <list type="bullet">
///   <item>If the action already carries a champion when it starts (the user hovered it, or declared it as pick intent
///   during PLANNING), it is the user's: do nothing (log <see cref="AutoPickLogKinds.Manual"/>).</item>
///   <item>Otherwise hover (<c>PATCH …/actions/{id} {championId}</c>) the first valid choice of the list and publish it as
///   <see cref="AutoPickRun.Pending"/>; none valid → log <see cref="AutoPickLogKinds.Skipped"/>, write nothing.</item>
///   <item>Valid ban: in <c>bannable-champion-ids</c>; not banned yet (either team); not hovered or banned by a teammate
///   (their ban action's champion); not a teammate's pick hover, lock or <c>championPickIntent</c>; not in the local
///   player's own resolved pick list.</item>
///   <item>Valid pick: in <c>pickable-champion-ids</c> (already only owned + free-to-play, not disabled); not banned (either
///   team); not hovered, locked or declared (<c>championPickIntent</c>) by a teammate; not locked by an enemy.</item>
///   <item>Complete in the turn's last second: <see cref="AutoPickPending.CompletesAt"/> = phase end −
///   <see cref="AutoPickState.CompletionMargin"/>, phase end = timer <c>internalNowInEpochMs</c> +
///   <c>adjustedTimeLeftInPhase</c> (the time left at that anchor; without one, now + it) (infinite timer: hover +
///   <see cref="AutoPickState.AutoLockDelay"/>). If that is not after the hover (too little time left), hover only and log
///   <see cref="AutoPickLogKinds.Hovered"/>.</item>
///   <item>Before completing, re-read the session: the action must still be in progress with the auto-hovered champion,
///   else the user changed it (log <see cref="AutoPickLogKinds.Manual"/>, never touch that action again) or it is over
///   (nothing). If the champion became invalid meanwhile, hover the next valid choice (completing at the same phase
///   end). Lock the same way <see cref="CommandType.LockChampion"/> does (PATCH completed, confirm,
///   <c>/complete</c> fallback) and log <see cref="AutoPickLogKinds.Banned"/> / <see cref="AutoPickLogKinds.Locked"/>, or
///   <see cref="AutoPickLogKinds.Failed"/>.</item>
///   <item><see cref="CommandType.CancelAutoPickAction"/>, <see cref="CommandType.PauseAutoPick"/> or disabling cancels the
///   completion; the hover stays (the user decides from there) and the action is not touched again.</item>
///   </list></item>
/// <item><b>Spells</b>: once per champ select, at the first session where automation applies (also ARAM): if the resolved
/// preset has spells and the local player's <c>spell1Id</c>/<c>spell2Id</c> (in that order) differ, <c>PATCH
/// …/session/my-selection</c>. After a position swap they are applied again for the new role, but only if the user hasn't
/// changed them since the automation set them.</item>
/// <item><b>Rune page</b>: with the spells, once: if the preset has a <see cref="AutoPickPreset.RunePageId"/> that is in
/// <c>/lol-perks/v1/pages</c> and isn't current, <c>PUT /lol-perks/v1/currentpage</c>; a deleted page is skipped (logged).</item>
/// <item><b>Unsupported</b> (no writes): Arena (CHERRY), Practice Tool, the other custom queues (SR All Random, Howling
/// Abyss, URF, …), TFT, rotating modes and any queue not listed in <see cref="AutoPickQueueGroups"/>.</item>
/// </list>
/// </summary>
/// <param name="Settings">The saved presets (persisted by the connector, survive restarts).</param>
/// <param name="PausedForThisGame">
/// Set by <see cref="CommandType.PauseAutoPick"/>: no writes in the current champ select or, when sent before one starts
/// (in the lobby, while searching or at the ready check), in the next one. Cleared when that champ select ends (the game
/// starts or someone dodges), when the gameflow moves back to "None" (leaving for the home screen; a pause sent while
/// already there is kept for the next champ select), when the League client disconnects, and by
/// <see cref="CommandType.ResumeAutoPick"/>. Not persisted.
/// </param>
/// <param name="Run">What the automation does in the current champ select; null outside champ select.</param>
/// <param name="UpdatedAt">Connector clock; see <see cref="AutoPickPending.CompletesAt"/> for the countdown.</param>
public sealed record AutoPickState(
    AutoPickSettings Settings,
    bool PausedForThisGame,
    AutoPickRun? Run,
    DateTimeOffset UpdatedAt)
{
    public static AutoPickState Empty() => new(AutoPickSettings.Default, false, null, DateTimeOffset.UtcNow);

    /// <summary>The wait between the auto-hover and the lock-in / ban when the timer never runs out (it has no last second).</summary>
    public static readonly TimeSpan AutoLockDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The automation completes an action this long before the phase timer runs out: the turn's last second, so the user
    /// has the whole turn to change or cancel it, with time left for the re-read and the lock request.
    /// </summary>
    public static readonly TimeSpan CompletionMargin = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long <see cref="AutoPickSettings.AutoAccept"/> waits after a ready check appears before accepting it: a moment to
    /// decline on the phone or in the client.
    /// </summary>
    public static readonly TimeSpan AutoAcceptDelay = TimeSpan.FromSeconds(2);

    /// <summary>Entries kept in <see cref="AutoPickRun.Log"/> (the oldest is dropped).</summary>
    public const int MaxLogEntries = 10;

    /// <summary>Longest <see cref="AutoPickLogEntry.Text"/>.</summary>
    public const int MaxLogTextLength = 80;
}

/// <summary>
/// The user's automation settings: a global switch and one preset per queue group and role. Persisted by the connector
/// (<c>%LOCALAPPDATA%\league-remote\auto-pick.json</c>), edited by the phone one preset at a time
/// (<see cref="CommandType.SetAutoPickPreset"/>, <see cref="CommandType.SetAutoPickEnabled"/>).
/// </summary>
/// <param name="Enabled">Master switch; off by default (nothing is automated until the user turns it on).</param>
/// <param name="Presets">
/// At most one per (<see cref="AutoPickPreset.QueueGroup"/>, <see cref="AutoPickPreset.Role"/>), never an empty one (setting
/// an empty preset removes it), ordered by group (<see cref="AutoPickQueueGroups.All"/>) then role
/// (<see cref="AutoPickRoles.All"/>). At most <see cref="MaxPresets"/>.
/// </param>
/// <param name="AutoAccept">
/// The connector accepts ready checks by itself (<see cref="CommandType.SetAutoAcceptEnabled"/>); off by default, also for a
/// file saved before this setting existed. Independent of <see cref="Enabled"/> and <see cref="AutoPickState.PausedForThisGame"/>.
/// The rules are on <c>AutoAcceptService</c> in the connector.
/// </param>
public sealed record AutoPickSettings(bool Enabled, IReadOnlyList<AutoPickPreset> Presets, bool AutoAccept = false)
{
    public static AutoPickSettings Default { get; } = new(false, []);

    /// <summary>Champions in <see cref="AutoPickPreset.BanChampionIds"/> (first valid one is banned, the rest are fallbacks).</summary>
    public const int MaxBans = 5;

    /// <summary>Champions in <see cref="AutoPickPreset.PickChampionIds"/> (first valid one is picked, the rest are fallbacks).</summary>
    public const int MaxPicks = 10;

    /// <summary>Every (group, role) pair that can have a preset: draft and blind with six roles each, ARAM with "ANY" only.</summary>
    public const int MaxPresets = 13;

    /// <summary>Longest <see cref="AutoPickPreset.RunePageName"/> kept (longer names are cut).</summary>
    public const int MaxRunePageNameLength = 64;

    /// <summary>
    /// The preset that applies in <paramref name="queueGroup"/> for <paramref name="role"/>, field by field: the role's own
    /// preset where it sets a field (a non-empty list, spells, a rune page), else the group's <see cref="AutoPickRoles.Any"/>
    /// preset. So "ANY" holds the defaults and a role preset overrides only what it sets. Null when neither exists.
    /// </summary>
    public AutoPickPreset? Resolve(string queueGroup, string role)
    {
        var any = Presets.FirstOrDefault(p => p.QueueGroup == queueGroup && p.Role == AutoPickRoles.Any);
        var own = role == AutoPickRoles.Any ? null : Presets.FirstOrDefault(p => p.QueueGroup == queueGroup && p.Role == role);
        if (own is null)
            return any;
        if (any is null)
            return own;
        var ownSpells = own.Spell1Id > 0 && own.Spell2Id > 0;
        return own with
        {
            BanChampionIds = own.BanChampionIds.Count > 0 ? own.BanChampionIds : any.BanChampionIds,
            PickChampionIds = own.PickChampionIds.Count > 0 ? own.PickChampionIds : any.PickChampionIds,
            Spell1Id = ownSpells ? own.Spell1Id : any.Spell1Id,
            Spell2Id = ownSpells ? own.Spell2Id : any.Spell2Id,
            RunePageId = own.RunePageId > 0 ? own.RunePageId : any.RunePageId,
            RunePageName = own.RunePageId > 0 ? own.RunePageName : any.RunePageName,
        };
    }
}

/// <summary>What to do in one queue group for one role.</summary>
/// <param name="QueueGroup">See <see cref="AutoPickQueueGroups"/>.</param>
/// <param name="Role">See <see cref="AutoPickRoles"/> (<see cref="AutoPickRoles.Any"/> only for ARAM).</param>
/// <param name="BanChampionIds">In priority order, distinct, at most <see cref="AutoPickSettings.MaxBans"/>; always empty for ARAM.</param>
/// <param name="PickChampionIds">In priority order, distinct, at most <see cref="AutoPickSettings.MaxPicks"/>; always empty for ARAM.</param>
/// <param name="Spell1Id">Summoner spell on D; 0 = leave the spells alone (then <see cref="Spell2Id"/> is 0 too).</param>
/// <param name="Spell2Id">Summoner spell on F; 0 = none.</param>
/// <param name="RunePageId">One of the user's rune pages (<see cref="RunePage.Id"/>); 0 = leave the page alone.</param>
/// <param name="RunePageName">
/// <see cref="RunePageId"/>'s name as the client showed it when the preset was saved (or last seen since), so the phone can
/// name the page while League is closed; "" when unknown (saved while the client wasn't running, or no page). Not part of the command: the connector fills it from the client's rune pages, never the phone. At most
/// <see cref="AutoPickSettings.MaxRunePageNameLength"/> characters. The user's own text, only ever shown to their phones.
/// </param>
public sealed record AutoPickPreset(
    string QueueGroup,
    string Role,
    IReadOnlyList<int> BanChampionIds,
    IReadOnlyList<int> PickChampionIds,
    int Spell1Id = 0,
    int Spell2Id = 0,
    long RunePageId = 0,
    string RunePageName = "")
{
    /// <summary>Nothing set: <see cref="CommandType.SetAutoPickPreset"/> with this removes the preset.</summary>
    [JsonIgnore]
    public bool IsEmpty => BanChampionIds.Count == 0 && PickChampionIds.Count == 0 && Spell1Id <= 0 && Spell2Id <= 0 && RunePageId <= 0;
}

/// <summary>
/// Groups of queues that share presets, by the champ-select session's <c>queueId</c> (ids from
/// <c>GET /lol-game-queues/v1/queues</c>, checked read-only against a live client in Oct 2026; 430 and 490 were not offered
/// then and are kept for when they return). Custom games are queues of their own there (<c>isCustom</c>, category
/// "Custom"): the Summoner's Rift Blind / Draft / Tournament Draft ones share the matching presets (no assigned roles, so
/// the <see cref="AutoPickRoles.Any"/> preset). Anything else is not automated.
/// </summary>
public static class AutoPickQueueGroups
{
    /// <summary>
    /// Draft Pick (400), Ranked Solo/Duo (420), Ranked Flex (440), Clash (700): assigned roles, bans, picks. Also the custom
    /// SR Draft Pick (3110, <c>TeamBuilderDraftPickStrategy</c>) and SR Tournament Draft (3130, <c>TournamentPickStrategy</c>).
    /// </summary>
    public const string Draft = "draft";

    /// <summary>
    /// Blind Pick (430), Swiftplay (480), Quickplay (490), Co-op vs. AI Intro/Beginner/Intermediate (870/880/890): picks
    /// and spells, no bans; usually no assigned role (then <see cref="AutoPickRoles.Any"/>). Swiftplay-style queues pick in
    /// the lobby and may have nothing left to do in champ select. Also the custom SR Blind Pick (3100, <c>SimulPickStrategy</c>;
    /// not Practice Tool 3140, which is Simul Pick too).
    /// </summary>
    public const string Blind = "blind";

    /// <summary>ARAM (450), ARAM Clash (720), ARAM: Mayhem (2400): random champions, so spells and rune page only.</summary>
    public const string Aram = "aram";

    /// <summary>In the order the phone shows them.</summary>
    public static readonly IReadOnlyList<string> All = [Draft, Blind, Aram];

    public static readonly IReadOnlyList<int> DraftQueueIds = [400, 420, 440, 700, 3110, 3130];
    public static readonly IReadOnlyList<int> BlindQueueIds = [430, 480, 490, 870, 880, 890, 3100];
    public static readonly IReadOnlyList<int> AramQueueIds = [450, 720, 2400];

    public static bool IsKnown(string? group) => group is not null && All.Contains(group);

    /// <summary>
    /// The group of a queue, or null when it isn't automated (Arena, Practice Tool and the other custom queues, TFT, rotating
    /// modes, unknown ids, no queue).
    /// </summary>
    public static string? ForQueue(int queueId) =>
        DraftQueueIds.Contains(queueId) ? Draft
        : BlindQueueIds.Contains(queueId) ? Blind
        : AramQueueIds.Contains(queueId) ? Aram
        : null;

    /// <summary>Whether the group's presets have ban and pick lists (not ARAM).</summary>
    public static bool HasChampionLists(string group) => group != Aram;
}

/// <summary>Roles a preset can be for: the five <see cref="Positions"/> (no FILL: champ select always assigns one) plus a fallback.</summary>
public static class AutoPickRoles
{
    /// <summary>Any role: used when no role is assigned, and for every field a role preset leaves unset.</summary>
    public const string Any = "ANY";

    /// <summary>In the order the phone shows them.</summary>
    public static readonly IReadOnlyList<string> All = [Any, Positions.Top, Positions.Jungle, Positions.Middle, Positions.Bottom, Positions.Utility];

    public static bool IsKnown(string? role) => role is not null && All.Contains(role);

    /// <summary>Roles allowed in <paramref name="group"/>: ARAM has no roles, only <see cref="Any"/>.</summary>
    public static bool IsAllowed(string group, string? role) => IsKnown(role) && (group != AutoPickQueueGroups.Aram || role == Any);

    /// <summary>The LCU's <c>assignedPosition</c> ("top", …, "utility"; "" when none) as a role; unknown values → <see cref="Any"/>.</summary>
    public static string FromAssignedPosition(string? assignedPosition)
    {
        var upper = (assignedPosition ?? "").Trim().ToUpperInvariant();
        return upper != Any && IsKnown(upper) ? upper : Any;
    }
}

/// <summary>The automation in the current champ select.</summary>
/// <param name="QueueGroup">The group whose presets apply; "" when this queue isn't automated.</param>
/// <param name="Role">The role whose preset applies (<see cref="AutoPickRoles"/>); "" when this queue isn't automated.</param>
/// <param name="Status">See <see cref="AutoPickRunStatus"/>.</param>
/// <param name="Pending">The auto-hovered ban or pick waiting to be completed; null when none.</param>
/// <param name="Log">What the automation did in this champ select, oldest first, at most <see cref="AutoPickState.MaxLogEntries"/>.</param>
/// <param name="Id">
/// Identifies this champ select's run: a new champ select (also after a dodge, or after the connector restarted) gets a
/// new, larger id, so the phone tells this run's log from an earlier one's (it toasts each new entry once per run). Opaque,
/// positive.
/// </param>
public sealed record AutoPickRun(
    string QueueGroup,
    string Role,
    string Status,
    AutoPickPending? Pending,
    IReadOnlyList<AutoPickLogEntry> Log,
    long Id);

/// <summary>Why the automation does or doesn't act in this champ select (<see cref="AutoPickRun.Status"/>).</summary>
public static class AutoPickRunStatus
{
    /// <summary>Running: it acts on the presets.</summary>
    public const string Active = "active";

    /// <summary><see cref="AutoPickSettings.Enabled"/> is off.</summary>
    public const string Disabled = "disabled";

    /// <summary><see cref="AutoPickState.PausedForThisGame"/>.</summary>
    public const string Paused = "paused";

    /// <summary>The queue isn't in any <see cref="AutoPickQueueGroups"/> (Arena, Practice Tool, …).</summary>
    public const string Unsupported = "unsupported";

    /// <summary>The group has no preset for this role nor for <see cref="AutoPickRoles.Any"/>.</summary>
    public const string NoPreset = "no-preset";
}

/// <summary>Champ-select action types the automation completes (the LCU's action <c>type</c>).</summary>
public static class AutoPickActionKinds
{
    public const string Ban = "ban";
    public const string Pick = "pick";
}

/// <summary>An auto-hovered action that completes unless the user cancels it or hovers something else.</summary>
/// <param name="Kind">See <see cref="AutoPickActionKinds"/>.</param>
/// <param name="ActionId">The LCU champ-select action (<see cref="PlayerAction.Id"/>); what <see cref="CommandType.CancelAutoPickAction"/> may name.</param>
/// <param name="ChampionId">The hovered champion.</param>
/// <param name="CompletesAt">
/// Connector clock. Count down from <c>CompletesAt − UpdatedAt</c> (both connector clock) starting when the update arrived;
/// don't compare it with the phone's clock, which can be seconds off.
/// </param>
/// <param name="Cancellable">False once the connector has started the lock-in / ban request.</param>
public sealed record AutoPickPending(string Kind, long ActionId, int ChampionId, DateTimeOffset CompletesAt, bool Cancellable = true);

/// <summary>One thing the automation did ("Auto-banned Zed").</summary>
/// <param name="At">Connector clock.</param>
/// <param name="Kind">See <see cref="AutoPickLogKinds"/>.</param>
/// <param name="ChampionId">The champion it is about; 0 for spells / rune page / nothing found.</param>
/// <param name="Text">
/// The connector's short English sentence, at most <see cref="AutoPickState.MaxLogTextLength"/> characters: champion names
/// from the client's catalog, never a player's name and never a rune page's name (the user typed those).
/// </param>
public sealed record AutoPickLogEntry(DateTimeOffset At, string Kind, int ChampionId, string Text);

/// <summary><see cref="AutoPickLogEntry.Kind"/> values.</summary>
public static class AutoPickLogKinds
{
    /// <summary>"Auto-banned Zed".</summary>
    public const string Banned = "banned";

    /// <summary>"Auto-locked Ahri".</summary>
    public const string Locked = "locked";

    /// <summary>"Hovered Ahri (too little time to lock in)": hovered, not completed.</summary>
    public const string Hovered = "hovered";

    /// <summary>"Cancelled auto-lock of Ahri": the phone cancelled or paused it.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>"You chose yourself, auto-pick stepped aside": the user hovered or locked something else.</summary>
    public const string Manual = "manual";

    /// <summary>"No champion from your pick list is available" / "Rune page no longer exists".</summary>
    public const string Skipped = "skipped";

    /// <summary>"Set your summoner spells".</summary>
    public const string Spells = "spells";

    /// <summary>"Switched to your rune page".</summary>
    public const string RunePage = "rune-page";

    /// <summary>"League didn't accept the auto-lock": an LCU write failed.</summary>
    public const string Failed = "failed";
}
