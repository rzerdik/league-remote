namespace LeagueRemote.Contracts;

/// <summary>
/// Validates a command's shape before the connector runs it (the phone checks the same rules first, so a mistake shows
/// at once). Returns null when valid, otherwise an error message. Whether the ids name something League lists right now
/// is up to the command executor.
/// </summary>
public static class CommandWhitelist
{
    /// <summary>Longest <see cref="RemoteCommand.RequestId"/> accepted.</summary>
    public const int MaxRequestIdLength = 64;

    /// <summary>Longest invitation id accepted. Puuids: <see cref="RiotIds.MaxPuuidLength"/>.</summary>
    public const int MaxLcuIdLength = 128;

    public static string? Validate(RemoteCommand? command)
    {
        if (command is null)
            return "Command is required.";
        if (string.IsNullOrWhiteSpace(command.RequestId) || command.RequestId.Length > MaxRequestIdLength)
            return $"RequestId is required (max {MaxRequestIdLength} chars).";
        if (!Enum.IsDefined(command.Type))
            return $"Command type '{(int)command.Type}' is not allowed.";

        return command.Type switch
        {
            CommandType.CreateLobby when command.QueueId is not > 0 => "QueueId must be a positive integer.",
            CommandType.HoverChampion or CommandType.LockChampion or CommandType.BenchSwap when command.ChampionId is not > 0
                => "ChampionId must be a positive integer.",
            CommandType.HoverChampion or CommandType.LockChampion when command.ActionId is not > 0
                => "ActionId must be a positive integer.",
            CommandType.SetSummonerSpells when command.Spell1Id is not > 0 || command.Spell2Id is not > 0 => "Spell1Id and Spell2Id must be positive integers.",
            CommandType.SetSummonerSpells when command.Spell1Id == command.Spell2Id => "Summoner spells must differ.",
            CommandType.SelectRunePage when command.RunePageId is not > 0 => "RunePageId must be a positive integer.",
            CommandType.RequestSwap or CommandType.AcceptSwap or CommandType.DeclineSwap or CommandType.CancelSwap
                when command.SwapKind is not { } kind || !Enum.IsDefined(kind) => "SwapKind must be PickOrder, Position or Trade.",
            CommandType.RequestSwap or CommandType.AcceptSwap or CommandType.DeclineSwap or CommandType.CancelSwap
                when command.SwapId is not > 0 => "SwapId must be a positive integer.",
            CommandType.SetPositionPreferences => ValidatePositions(command.FirstPosition, command.SecondPosition),
            CommandType.SendChatMessage or CommandType.MarkConversationRead when !IsChatId(command.ConversationId, ChatState.MaxConversationIdLength)
                => $"ConversationId is required (max {ChatState.MaxConversationIdLength} chars).",
            CommandType.SendChatMessage => ValidateChatBody(command.Body),
            CommandType.AcceptInvitation or CommandType.DeclineInvitation when !IsInvitationId(command.InvitationId)
                => $"InvitationId is required (max {MaxLcuIdLength} chars: letters, digits, '-', '_', '.', ':' or '@').",
            CommandType.RemoveFriend when !IsChatId(command.FriendId, FriendsState.MaxFriendIdLength)
                => $"FriendId is required (max {FriendsState.MaxFriendIdLength} chars).",
            CommandType.SendFriendRequest or CommandType.SearchPlayer
                => RiotIds.ValidateGameName(command.GameName) ?? RiotIds.ValidateTagLine(command.TagLine),
            CommandType.SendInvitation or CommandType.OpenDirectConversation
                or CommandType.AcceptFriendRequest or CommandType.DeclineFriendRequest or CommandType.CancelFriendRequest
                or CommandType.LoadPlayerProfile
                when !RiotIds.IsPuuid(command.Puuid) => $"Puuid is required (max {RiotIds.MaxPuuidLength} chars).",
            CommandType.LoadMatchDetails when command.GameId is not > 0 => "GameId must be a positive integer.",
            // Optional here: empty or missing = a game of the local player's own history.
            CommandType.LoadMatchDetails when command.Puuid is { Length: > 0 } && !RiotIds.IsPuuid(command.Puuid)
                => $"Puuid must be a puuid (max {RiotIds.MaxPuuidLength} chars).",
            CommandType.SetAutoPickEnabled when command.Enabled is null => "Enabled is required.",
            CommandType.SetAutoPickPreset => ValidateAutoPickPreset(command),
            CommandType.CancelAutoPickAction when command.ActionId is <= 0 => "ActionId must be a positive integer.",
            CommandType.SelectSkin when command.SkinId is not > 0 => "SkinId must be a positive integer.",
            _ => null,
        };
    }

    /// <summary>
    /// One preset: a known group and a role it allows, bounded lists of distinct positive champion ids (none for ARAM),
    /// both spells or neither, an optional rune page. Whether the champions, spells and page exist is up to League.
    /// </summary>
    private static string? ValidateAutoPickPreset(RemoteCommand command)
    {
        var group = command.QueueGroup;
        if (!AutoPickQueueGroups.IsKnown(group))
            return "QueueGroup must be draft, blind or aram.";
        if (!AutoPickRoles.IsAllowed(group!, command.Role))
            return group == AutoPickQueueGroups.Aram
                ? "Role must be ANY for aram."
                : "Role must be ANY, TOP, JUNGLE, MIDDLE, BOTTOM or UTILITY.";

        var bans = command.BanChampionIds ?? [];
        var picks = command.PickChampionIds ?? [];
        if (!AutoPickQueueGroups.HasChampionLists(group!) && (bans.Count > 0 || picks.Count > 0))
            return "ARAM presets have no ban or pick list.";
        if (ValidateChampionList(bans, AutoPickSettings.MaxBans, "BanChampionIds") is { } banError)
            return banError;
        if (ValidateChampionList(picks, AutoPickSettings.MaxPicks, "PickChampionIds") is { } pickError)
            return pickError;

        if (command.Spell1Id is not null || command.Spell2Id is not null)
        {
            if (command.Spell1Id is not > 0 || command.Spell2Id is not > 0)
                return "Spell1Id and Spell2Id must both be positive integers, or both empty.";
            if (command.Spell1Id == command.Spell2Id)
                return "Summoner spells must differ.";
        }

        return command.RunePageId is <= 0 ? "RunePageId must be a positive integer." : null;
    }

    private static string? ValidateChampionList(IReadOnlyList<int> ids, int max, string name)
    {
        if (ids.Count > max)
            return $"{name} can have at most {max} champions.";
        if (ids.Any(id => id <= 0))
            return $"{name} must be positive integers.";
        return ids.Distinct().Count() != ids.Count ? $"{name} must not repeat a champion." : null;
    }

    /// <summary>Two different roles, or FILL alone (the client hides the second choice when the first is FILL).</summary>
    private static string? ValidatePositions(string? first, string? second)
    {
        if (!Positions.IsSelectable(first))
            return "FirstPosition must be TOP, JUNGLE, MIDDLE, BOTTOM, UTILITY or FILL.";
        if (first == Positions.Fill)
            return second is null or Positions.Unselected ? null : "SecondPosition must be empty when FirstPosition is FILL.";
        if (!Positions.IsSelectable(second))
            return "SecondPosition must be TOP, JUNGLE, MIDDLE, BOTTOM, UTILITY or FILL.";
        return first == second ? "The two positions must differ." : null;
    }

    /// <summary>A chat id (conversation or friend: <c>{puuid}@eu2.pvp.net</c>, room ids): one line, at most <paramref name="max"/>.</summary>
    private static bool IsChatId(string? id, int max) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= max && !id.Any(char.IsControl);

    /// <summary>
    /// An LCU invitation id: ASCII letters, digits and <c>- _ . : @</c> only. The connector only acts on ids the
    /// client lists right now, so this just keeps odd input (paths, whitespace) out.
    /// </summary>
    private static bool IsInvitationId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= MaxLcuIdLength && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':' or '@');

    /// <summary>
    /// One line of 1–<see cref="ChatState.MaxMessageLength"/> characters that isn't only whitespace (what the client's chat
    /// input allows).
    /// </summary>
    private static string? ValidateChatBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "Body is required.";
        if (body.Length > ChatState.MaxMessageLength)
            return $"Body must be at most {ChatState.MaxMessageLength} characters.";
        return body.Any(char.IsControl) ? "Body must be a single line of text." : null;
    }
}
