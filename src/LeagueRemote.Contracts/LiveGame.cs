namespace LeagueRemote.Contracts;

/// <summary>
/// The running game as the phone's In Game screen shows it: your stats, the scoreboard of both teams, objective counts,
/// objective timers and a kill/objective feed. Built from Riot's official Live Client Data API
/// (<c>GET https://127.0.0.1:2999/liveclientdata/allgamedata</c>, served by the game process while a game runs; read-only,
/// nothing is ever sent to the game). Published as its own hub message (<see cref="HubMethods.Client.LiveGameUpdated"/>)
/// because it churns every few seconds; never pushed. <see cref="InGameState"/> in <see cref="ClientState"/> stays the
/// source for "game loaded" and its push trigger.
/// <para>
/// Privacy: like champ select, the enemy team is shown only as the game's own scoreboard shows it, by champion. Riot IDs
/// (<see cref="LivePlayer.GameName"/> / <see cref="LivePlayer.TagLine"/>, the <c>…GameName</c> fields of
/// <see cref="LiveEvent"/>) are set only for the local player's team and only when that team is known
/// (<see cref="LiveTeam.IsMyTeam"/>) and has at most <see cref="MaxTeamSize"/> players; otherwise only the local player
/// keeps a name. There are no puuids or summoner ids (the API has none).
/// </para>
/// </summary>
/// <param name="Available">
/// True while the API answers with a game; false in <see cref="Empty"/>, which the connector publishes when the game ends
/// (the gameflow phase leaves GameStart / InProgress / Reconnect) or before the API answers.
/// </param>
/// <param name="GameTime">
/// In-game clock in seconds at <see cref="UpdatedAt"/> (<c>gameData.gameTime</c>, the clock the game shows). The phone
/// interpolates between updates. Every other time in this slice (<see cref="LiveEvent.Time"/>, the timers) is on this clock.
/// </param>
/// <param name="GameMode"><c>gameData.gameMode</c>: "CLASSIC", "ARAM", "CHERRY" (Arena), "PRACTICETOOL", "URF", "SWIFTPLAY", …</param>
/// <param name="MapNumber"><c>gameData.mapNumber</c>: 11 Summoner's Rift, 12 Howling Abyss, 30 Arena, …; 0 when unknown.</param>
/// <param name="Me">
/// The local player (<c>activePlayer</c>, matched to its <c>allPlayers</c> line by Riot ID); null when the API has no
/// active player (spectating) or the line can't be matched. Then no team is <see cref="LiveTeam.IsMyTeam"/> and nobody is named.
/// </param>
/// <param name="Teams">
/// One per <c>team</c> value in <c>allPlayers</c>: the local player's team first, then the others (ORDER before CHAOS).
/// Empty while unavailable.
/// </param>
/// <param name="Timers">
/// Next-spawn timers; null outside Summoner's Rift's normal and ranked games (<see cref="LiveGameRules.HasObjectiveTimers"/>)
/// and while unavailable.
/// </param>
/// <param name="Events">
/// The newest <see cref="MaxEvents"/> feed events, in game order (oldest first). Counts in <see cref="LiveTeam"/> and the
/// timers are derived from the game's whole event list, not just these.
/// </param>
/// <param name="Result">
/// <see cref="MatchResults.Win"/> or <see cref="MatchResults.Loss"/> once the API reports <c>GameEnd</c> (the local
/// player's result); empty before.
/// </param>
public sealed record LiveGameState(
    bool Available,
    double GameTime,
    string GameMode,
    int MapNumber,
    LiveActivePlayer? Me,
    IReadOnlyList<LiveTeam> Teams,
    LiveObjectiveTimers? Timers,
    IReadOnlyList<LiveEvent> Events,
    string Result,
    DateTimeOffset UpdatedAt)
{
    public static LiveGameState Empty() => new(false, 0, "", 0, null, [], null, [], "", DateTimeOffset.UtcNow);

    /// <summary>Feed events kept in <see cref="Events"/>.</summary>
    public const int MaxEvents = 30;

    /// <summary>
    /// A team with more players than this (an unexpected mode, e.g. Arena reporting all players on one side) gets no names
    /// except the local player's, so enemies can never be named by accident.
    /// </summary>
    public const int MaxTeamSize = 5;
}

/// <summary>The local player's extras from <c>activePlayer</c>; their scoreboard line is <see cref="Player"/>.</summary>
/// <param name="Player">The same line as the <see cref="LivePlayer.IsMe"/> entry in <see cref="LiveGameState.Teams"/>.</param>
/// <param name="CurrentGold">Unspent gold (<c>activePlayer.currentGold</c>; the API reports it only for the local player).</param>
public sealed record LiveActivePlayer(LivePlayer Player, double CurrentGold);

/// <summary>Team names as the Live Client Data API reports them (<c>allPlayers[].team</c>, <see cref="LiveTeam.Side"/>).</summary>
public static class LiveTeams
{
    /// <summary>Blue side (bottom left); LCU team 100; structure names <c>…_T1_…</c>, minions <c>Minion_T100…</c>.</summary>
    public const string Order = "ORDER";

    /// <summary>Red side (top right); LCU team 200; structure names <c>…_T2_…</c>, minions <c>Minion_T200…</c>.</summary>
    public const string Chaos = "CHAOS";
}

/// <summary>
/// One team's scoreboard and objectives. The objective counts are derived from the game's whole event list (the API has no
/// per-team totals).
/// </summary>
/// <param name="Side"><see cref="LiveTeams"/> value (other strings are passed through).</param>
/// <param name="IsMyTeam">The local player's team; false for every team when <see cref="LiveGameState.Me"/> is null.</param>
/// <param name="Kills">Sum of the players' <see cref="LivePlayer.Kills"/> (executions by turrets or minions aren't anyone's kill).</param>
/// <param name="Towers">
/// Enemy turrets this team destroyed (<c>TurretKilled</c>; counted by the turret's owner in its name, so a turret that falls
/// to minions counts too).
/// </param>
/// <param name="Inhibitors">Enemy inhibitors this team destroyed (<c>InhibKilled</c>, counted like <see cref="Towers"/>).</param>
/// <param name="Dragons">
/// Elemental drakes this team slew, as <see cref="LiveDragonTypes"/>, in kill order (four = the dragon soul). Elder
/// Dragons are counted in <see cref="Elders"/> instead.
/// </param>
/// <param name="Elders">Elder Dragons slain.</param>
/// <param name="Barons">Baron Nashor kills (<c>BaronKill</c>).</param>
/// <param name="Heralds">Rift Herald kills (<c>HeraldKill</c>).</param>
/// <param name="Grubs">Voidgrubs slain (<c>HordeKill</c>, one event per grub).</param>
/// <param name="Players">In the API's order (verify live whether that is top, jungle, mid, bottom, support).</param>
public sealed record LiveTeam(
    string Side,
    bool IsMyTeam,
    int Kills,
    int Towers,
    int Inhibitors,
    IReadOnlyList<string> Dragons,
    int Elders,
    int Barons,
    int Heralds,
    int Grubs,
    IReadOnlyList<LivePlayer> Players);

/// <summary>
/// One <c>allPlayers</c> entry: what the in-game scoreboard (Tab) shows. See <see cref="LiveGameState"/> for when
/// <see cref="GameName"/> / <see cref="TagLine"/> are set.
/// </summary>
/// <param name="ChampionId">
/// Numeric champion id like everywhere else in the contracts; the API has none, so the connector maps the internal name in
/// <c>rawChampionName</c> (<c>game_character_displayname_MonkeyKing</c> → <c>MonkeyKing</c>) to the <c>alias</c> in the LCU's
/// <c>/lol-game-data/assets/v1/champion-summary.json</c> (MonkeyKing → 62), falling back to its <c>name</c>. 0 when unknown.
/// </param>
/// <param name="ChampionName">The API's <c>championName</c> (display name in the game's language, e.g. "Wukong"); shown when the id is 0.</param>
/// <param name="IsMe">The local player.</param>
/// <param name="IsBot">A bot (<c>isBot</c>).</param>
/// <param name="GameName">Riot ID game name (<c>riotIdGameName</c>); own team only, empty for enemies.</param>
/// <param name="TagLine">Riot ID tag line without '#' (<c>riotIdTagLine</c>); own team only, empty for enemies.</param>
/// <param name="Position">
/// A <see cref="Positions"/> value (TOP, JUNGLE, MIDDLE, BOTTOM, UTILITY) from <c>position</c>; empty when the API sends
/// "", "NONE" or "UNKNOWN" (it often does outside ranked and draft).
/// </param>
/// <param name="Level">Champion level.</param>
/// <param name="CreepScore"><c>scores.creepScore</c> (minions + monsters; the API updates it in steps of 10).</param>
/// <param name="WardScore"><c>scores.wardScore</c> (vision score, fractional).</param>
/// <param name="Items">The filled inventory slots, by <see cref="LiveItem.Slot"/>.</param>
/// <param name="Spell1">Summoner spell on D (<c>summonerSpells.summonerSpellOne</c>).</param>
/// <param name="Spell2">Summoner spell on F (<c>summonerSpellTwo</c>).</param>
/// <param name="Keystone">Keystone rune id (<c>runes.keystone.id</c>, e.g. 8112 Electrocute); 0 when none.</param>
/// <param name="PrimaryStyle">Primary rune tree id (<c>runes.primaryRuneTree.id</c>); 0 when none.</param>
/// <param name="SubStyle">Secondary rune tree id (<c>runes.secondaryRuneTree.id</c>); 0 when none.</param>
/// <param name="IsDead"><c>isDead</c>.</param>
/// <param name="RespawnSeconds">Seconds until respawn at <see cref="LiveGameState.UpdatedAt"/> (<c>respawnTimer</c>); 0 when alive.</param>
public sealed record LivePlayer(
    int ChampionId,
    string ChampionName,
    bool IsMe,
    bool IsBot,
    string GameName,
    string TagLine,
    string Position,
    int Level,
    int Kills,
    int Deaths,
    int Assists,
    int CreepScore,
    double WardScore,
    IReadOnlyList<LiveItem> Items,
    LiveSummonerSpell Spell1,
    LiveSummonerSpell Spell2,
    int Keystone,
    int PrimaryStyle,
    int SubStyle,
    bool IsDead,
    double RespawnSeconds);

/// <summary>One <c>items[]</c> entry.</summary>
/// <param name="ItemId"><c>itemID</c>: Data Dragon item id (<c>item.json</c>).</param>
/// <param name="Slot"><c>slot</c>: 0–5 inventory, 6 trinket.</param>
/// <param name="Count"><c>count</c>: stack size (potions, wards); 1 for most items.</param>
public sealed record LiveItem(int ItemId, int Slot, int Count);

/// <summary>A summoner spell. The API sends no id, only names.</summary>
/// <param name="Id">
/// Data Dragon spell id (<c>summoner.json</c> <c>id</c>, e.g. "SummonerFlash", "SummonerDot" for Ignite), parsed from
/// <c>rawDisplayName</c> (<c>GeneratedTip_SummonerSpell_SummonerFlash_DisplayName</c>); empty when it doesn't parse.
/// </param>
/// <param name="Name"><c>displayName</c> in the game's language (e.g. "Flash"); shown when <see cref="Id"/> is unknown to the phone.</param>
public sealed record LiveSummonerSpell(string Id, string Name)
{
    public static readonly LiveSummonerSpell None = new("", "");
}

/// <summary>
/// Dragon types as the API names them (<c>DragonKill.DragonType</c>), passed through as is. The phone shows the drake names
/// in brackets.
/// </summary>
public static class LiveDragonTypes
{
    /// <summary>Infernal Drake.</summary>
    public const string Fire = "Fire";

    /// <summary>Mountain Drake.</summary>
    public const string Earth = "Earth";

    /// <summary>Ocean Drake.</summary>
    public const string Water = "Water";

    /// <summary>Cloud Drake.</summary>
    public const string Air = "Air";

    /// <summary>Hextech Drake.</summary>
    public const string Hextech = "Hextech";

    /// <summary>Chemtech Drake.</summary>
    public const string Chemtech = "Chemtech";

    /// <summary>The Elder Dragon (after a team's soul).</summary>
    public const string Elder = "Elder";
}

/// <summary>Lanes of a structure, from its name (<c>Turret_T2_R_03_A</c>, <c>Barracks_T1_L1</c>: L top, C mid, R bot).</summary>
public static class LiveLanes
{
    public const string Top = "top";

    /// <summary>Also the two Nexus turrets (<c>Turret_T1_C_01_A</c> / <c>_02_A</c>).</summary>
    public const string Mid = "mid";

    public const string Bot = "bot";
}

/// <summary>
/// Objective timers on <see cref="LiveGameState.GameTime"/>'s clock, so the phone counts down without drift between
/// updates. Derived from the game's whole event list and <see cref="LiveGameRules"/> on every poll (so a reconnect or a
/// late connector start still gets them right).
/// </summary>
/// <param name="Dragon">The dragon pit: elemental drakes, then the Elder Dragon once a team has its soul.</param>
/// <param name="Baron">Baron Nashor.</param>
/// <param name="Herald">The Rift Herald (one spawn per game).</param>
/// <param name="Grubs">The Voidgrub camp (three grubs, one spawn per game).</param>
/// <param name="Inhibitors">Destroyed inhibitors that will respawn, soonest first; empty when none is down.</param>
public sealed record LiveObjectiveTimers(
    LiveObjectiveTimer Dragon,
    LiveObjectiveTimer Baron,
    LiveObjectiveTimer Herald,
    LiveObjectiveTimer Grubs,
    IReadOnlyList<LiveInhibitorTimer> Inhibitors);

/// <summary>Objective kinds (<see cref="LiveObjectiveTimer.Kind"/>).</summary>
public static class LiveObjectiveKinds
{
    public const string Dragon = "dragon";

    /// <summary>The dragon pit once a team has four drakes.</summary>
    public const string Elder = "elder";

    public const string Baron = "baron";

    public const string Herald = "herald";

    public const string Grubs = "grubs";
}

/// <summary>
/// When an epic monster is next up. The API doesn't say whether a monster is alive; this is inferred from the rules and the
/// kill events: from <see cref="SpawnsAt"/> on it is up until a kill event moves <see cref="SpawnsAt"/> on (or to null).
/// </summary>
/// <param name="Kind"><see cref="LiveObjectiveKinds"/> value.</param>
/// <param name="SpawnsAt">
/// Game time (seconds) it spawns or respawns; at or before <see cref="LiveGameState.GameTime"/> it is up. Null when it
/// won't spawn again this game (the Herald or the grub camp was taken, or <see cref="DespawnsAt"/> has passed).
/// </param>
/// <param name="DespawnsAt">
/// Game time it leaves for good if nobody takes it (Herald, grubs; see <see cref="LiveGameRules"/>); null for the others.
/// </param>
/// <param name="DragonType">
/// Dragon pit only: the next drake's <see cref="LiveDragonTypes"/> value when known, else empty. The API only names a drake
/// when it dies; from the third drake on every drake has the Elemental Rift's element, so it is known after the third kill
/// (and <see cref="LiveDragonTypes.Elder"/> when <see cref="Kind"/> is <see cref="LiveObjectiveKinds.Elder"/>).
/// </param>
public sealed record LiveObjectiveTimer(string Kind, double? SpawnsAt, double? DespawnsAt, string DragonType);

/// <summary>A destroyed inhibitor (<c>InhibKilled</c>) until it respawns (<c>InhibRespawned</c> or its time passed).</summary>
/// <param name="Side">The team that owns the inhibitor (whose base it is in), from its name (<c>Barracks_T2_R1</c> → CHAOS).</param>
/// <param name="Lane"><see cref="LiveLanes"/> value; empty when the name doesn't parse.</param>
/// <param name="RespawnsAt">Game time it respawns: kill time + <see cref="LiveGameRules.InhibitorRespawn"/>.</param>
public sealed record LiveInhibitorTimer(string Side, string Lane, double RespawnsAt);

/// <summary>
/// Spawn rules behind <see cref="LiveObjectiveTimers"/>, in game-clock seconds. They follow the current patch and must be
/// revisited when Riot changes the map (season starts and big objective patches). Checked Oct 2026 (patch 26.x) against the
/// League of Legends Wiki's unit pages and their patch histories (Dragon pit, Elder Dragon, Baron Nashor, Rift Herald,
/// Voidgrub camp, Inhibitor at wiki.leagueoflegends.com).
/// </summary>
public static class LiveGameRules
{
    /// <summary>Summoner's Rift's map number (<c>gameData.mapNumber</c>).</summary>
    public const int SummonersRift = 11;

    /// <summary>
    /// Timers are computed for Summoner's Rift in the CLASSIC mode (normal, ranked and custom draft/blind games). Other modes
    /// (ARAM, Arena, URF, Swiftplay, Practice Tool) have different rules and get null.
    /// </summary>
    public static bool HasObjectiveTimers(string? gameMode, int mapNumber) =>
        mapNumber == SummonersRift && string.Equals(gameMode, "CLASSIC", StringComparison.Ordinal);

    /// <summary>First elemental drake: 5:00.</summary>
    public const double DragonFirstSpawn = 5 * 60;

    /// <summary>Next drake 5:00 after a drake dies.</summary>
    public const double DragonRespawn = 5 * 60;

    /// <summary>Drakes a team needs for its dragon soul; the pit then spawns only the Elder Dragon.</summary>
    public const int DragonSoulDrakes = 4;

    /// <summary>
    /// Elder Dragon 6:00 after the drake that gave a team its soul, and 6:00 after each Elder kill (the wiki: "Once a team has
    /// killed their fourth drake, the camp will now instead spawn the Elder Dragon with a 6 minute respawn timer").
    /// </summary>
    public const double ElderSpawnDelay = 6 * 60;

    /// <summary>Voidgrubs: one camp of three at 8:00 (patch 26.1 removed the second wave), gone at 14:45 if not all slain.</summary>
    public const double GrubsSpawn = 8 * 60;

    /// <inheritdoc cref="GrubsSpawn"/>
    public const double GrubsDespawn = 14 * 60 + 45;

    /// <summary>Grubs in the camp; the camp is taken once this many <c>HordeKill</c> events came in.</summary>
    public const int GrubsPerCamp = 3;

    /// <summary>Rift Herald: once, at 15:00 (patch 25.09); leaves at 19:45 if not slain. Doesn't respawn.</summary>
    public const double HeraldSpawn = 15 * 60;

    /// <inheritdoc cref="HeraldSpawn"/>
    public const double HeraldDespawn = 19 * 60 + 45;

    /// <summary>Baron Nashor at 20:00 (back from 25:00 in patch 26.1, when Atakhan was removed), 6:00 after each kill.</summary>
    public const double BaronSpawn = 20 * 60;

    /// <inheritdoc cref="BaronSpawn"/>
    public const double BaronRespawn = 6 * 60;

    /// <summary>Inhibitors respawn 5:00 after they fall.</summary>
    public const double InhibitorRespawn = 5 * 60;
}

/// <summary>
/// Feed event kinds (<see cref="LiveEvent.Kind"/>), one per API event (<c>EventName</c> in brackets). GameStart,
/// MinionsSpawning, InhibRespawningSoon and unknown events are not in the feed.
/// </summary>
public static class LiveEventKinds
{
    /// <summary>A champion died (<c>ChampionKill</c>).</summary>
    public const string Kill = "kill";

    /// <summary>The game's first kill (<c>FirstBlood</c>, right after its <see cref="Kill"/>; actor = <c>Recipient</c>).</summary>
    public const string FirstBlood = "firstBlood";

    /// <summary>Double kill or more (<c>Multikill</c>, <see cref="LiveEvent.KillStreak"/> 2–5).</summary>
    public const string Multikill = "multikill";

    /// <summary>A team killed all five enemies (<c>Ace</c>; actor = <c>Acer</c>, side = <c>AcingTeam</c>).</summary>
    public const string Ace = "ace";

    /// <summary>A drake or the Elder Dragon (<c>DragonKill</c>, <see cref="LiveEvent.DragonType"/>).</summary>
    public const string Dragon = "dragon";

    /// <summary><c>BaronKill</c>.</summary>
    public const string Baron = "baron";

    /// <summary><c>HeraldKill</c>.</summary>
    public const string Herald = "herald";

    /// <summary>One Voidgrub (<c>HordeKill</c>; three per camp).</summary>
    public const string Grub = "grub";

    /// <summary>A turret fell (<c>TurretKilled</c>).</summary>
    public const string Turret = "turret";

    /// <summary>The game's first turret (<c>FirstBrick</c>, right after its <see cref="Turret"/>).</summary>
    public const string FirstTower = "firstTower";

    /// <summary>An inhibitor fell (<c>InhibKilled</c>).</summary>
    public const string Inhibitor = "inhibitor";

    /// <summary>An inhibitor is back (<c>InhibRespawned</c>; side = its owner).</summary>
    public const string InhibitorRespawned = "inhibitorRespawned";

    /// <summary>The Nexus fell (<c>GameEnd</c>; <see cref="LiveEvent.Result"/> is the local player's result).</summary>
    public const string GameEnd = "gameEnd";
}

/// <summary>
/// One feed line from <c>events.Events[]</c>. The API names players by Riot game name (<c>KillerName</c>,
/// <c>VictimName</c>, <c>Assisters</c>, <c>Recipient</c>, <c>Acer</c>); the connector resolves each to its
/// <c>allPlayers</c> line (<c>riotIdGameName</c>, then <c>riotId</c>, then <c>summonerName</c>; a name two players share
/// resolves to nobody) and sends champions, never an enemy's name. Non-champion killers (<c>Turret_T1_L_03_A</c>,
/// <c>Minion_T200…</c>, monsters) give no champion; their side comes from the name where it says (T1/T100 ORDER, T2/T200 CHAOS).
/// </summary>
/// <param name="Id"><c>EventID</c>: unique and increasing within a game (use it as the list key).</param>
/// <param name="Time"><c>EventTime</c>: game-clock seconds.</param>
/// <param name="Kind"><see cref="LiveEventKinds"/> value.</param>
/// <param name="Side">
/// <see cref="LiveTeams"/> value of the team that scored: the killer's team (a kill by a turret or minion: the victim's
/// enemies), the team that destroyed a structure (the opposite of its owner), the acing team; for
/// <see cref="LiveEventKinds.InhibitorRespawned"/> the inhibitor's owner. Empty when unknown and for
/// <see cref="LiveEventKinds.GameEnd"/>.
/// </param>
/// <param name="IsMyTeam"><see cref="Side"/> is the local player's team.</param>
/// <param name="ActorChampionId">The champion who did it (killer, first blood, multikill, acer, objective or structure killer); 0 when none or not a champion.</param>
/// <param name="ActorChampionName">Its <see cref="LivePlayer.ChampionName"/>; empty when none.</param>
/// <param name="ActorGameName">The actor's Riot game name; own team only (as in <see cref="LivePlayer.GameName"/>), else empty.</param>
/// <param name="VictimChampionId">Kills only: the champion who died; 0 otherwise.</param>
/// <param name="VictimChampionName">Its <see cref="LivePlayer.ChampionName"/>; empty when none.</param>
/// <param name="VictimGameName">The victim's Riot game name; own team only, else empty.</param>
/// <param name="AssistChampionIds">Champions in <c>Assisters</c> that resolve to a player, in the API's order; ids only, no names.</param>
/// <param name="KillStreak"><see cref="LiveEventKinds.Multikill"/>: 2 double … 5 penta; 0 otherwise.</param>
/// <param name="DragonType"><see cref="LiveEventKinds.Dragon"/>: a <see cref="LiveDragonTypes"/> value; empty otherwise.</param>
/// <param name="Stolen">Dragon, Baron, Herald, grub: <c>Stolen</c> (the API sends the string "True" / "False").</param>
/// <param name="Lane">Turret and inhibitor events: <see cref="LiveLanes"/> value from the structure's name; empty otherwise.</param>
/// <param name="Result"><see cref="LiveEventKinds.GameEnd"/>: <see cref="MatchResults.Win"/> or <see cref="MatchResults.Loss"/> (API "Win" / "Lose"); empty otherwise.</param>
public sealed record LiveEvent(
    int Id,
    double Time,
    string Kind,
    string Side,
    bool IsMyTeam,
    int ActorChampionId,
    string ActorChampionName,
    string ActorGameName,
    int VictimChampionId,
    string VictimChampionName,
    string VictimGameName,
    IReadOnlyList<int> AssistChampionIds,
    int KillStreak,
    string DragonType,
    bool Stolen,
    string Lane,
    string Result);
