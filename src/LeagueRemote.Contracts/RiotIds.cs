namespace LeagueRemote.Contracts;

/// <summary>
/// Riot ID rules for <see cref="CommandType.SendFriendRequest"/> and <see cref="CommandType.SearchPlayer"/> (what Riot
/// allows when you pick a Riot ID), and the shape of a puuid the commands accept.
/// </summary>
public static class RiotIds
{
    public const int MinGameNameLength = 3;
    public const int MaxGameNameLength = 16;
    public const int MinTagLineLength = 3;
    public const int MaxTagLineLength = 5;

    /// <summary>Puuids are GUIDs today; older ones are longer base64url strings.</summary>
    public const int MaxPuuidLength = 128;

    /// <summary>Null when valid. Callers trim first; spaces inside a game name are allowed (as in Riot IDs), '#' is not.</summary>
    public static string? ValidateGameName(string? gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return "Enter a game name.";
        if (gameName.Length is < MinGameNameLength or > MaxGameNameLength)
            return $"Game names are {MinGameNameLength}–{MaxGameNameLength} characters.";
        return gameName.Any(c => char.IsControl(c) || c == '#') || gameName != gameName.Trim()
            ? "That game name isn't valid."
            : null;
    }

    /// <summary>Null when valid: 3–5 letters or digits, without '#'.</summary>
    public static string? ValidateTagLine(string? tagLine)
    {
        if (string.IsNullOrWhiteSpace(tagLine))
            return "Enter a tag line (the part after #).";
        if (tagLine.Length is < MinTagLineLength or > MaxTagLineLength)
            return $"Tag lines are {MinTagLineLength}–{MaxTagLineLength} characters.";
        return tagLine.All(char.IsLetterOrDigit) ? null : "Tag lines are letters and digits only.";
    }

    /// <summary>Letters, digits, '-' and '_' only: a puuid becomes part of LCU paths and chat ids.</summary>
    public static bool IsPuuid(string? puuid) =>
        !string.IsNullOrEmpty(puuid) && puuid.Length <= MaxPuuidLength && puuid.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
