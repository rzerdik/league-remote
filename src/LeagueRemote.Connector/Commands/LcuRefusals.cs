using LeagueRemote.Connector.Lcu;

namespace LeagueRemote.Connector.Commands;

/// <summary>
/// What the phone shows when League refuses a command's request. League's own text is never passed on: it can be raw JSON,
/// an internal path or a player's name, and it's plumbing. It only picks one of these fixed sentences.
/// </summary>
internal static class LcuRefusals
{
    /// <summary>
    /// The sentence for any command without a more specific message: by status, plus a reason when League's error
    /// <c>message</c> / <c>errorCode</c> says something we recognise.
    /// </summary>
    public static string Message(LcuRequestException ex)
    {
        var status = (int)ex.StatusCode;
        if (status == 404)
            return "That's no longer there.";
        if (status == 429)
            return "League is busy. Wait a moment and try again.";

        // The LCU wraps the platform's refusals as 500 more often than not, so a recognised reason wins over the status.
        var reason = KnownReason(LeagueText(ex));
        if (reason is not null)
            return $"League refused that. {reason}";
        return status >= 500 ? "League couldn't do that. Try again." : "League refused that.";
    }

    /// <summary>
    /// The client's own wording for a failed Add Friend (rcp-fe-lol-social <c>getAddFriendErrorTranslationKey</c>): by status,
    /// and for 409/500 by the reason League gives.
    /// </summary>
    public static string FriendRequestError(LcuRequestException ex)
    {
        var reason = LeagueText(ex);
        bool Says(string text) => reason.Contains(text, StringComparison.OrdinalIgnoreCase);
        return (int)ex.StatusCode switch
        {
            400 => "That Riot ID isn't valid.",
            403 => "You can't add yourself.",
            404 => "No player with that Riot ID.",
            405 => "You blocked that player. Unblock them in the client first.",
            409 when Says("already-on-roster") => "You're already friends.",
            409 when Says("blocked") => "You blocked that player. Unblock them in the client first.",
            409 when Says("this-invite") => "You can't add yourself.",
            409 when Says("max-invitee") => "That player has too many pending friend requests.",
            409 when Says("max-inviter") => "You have too many pending friend requests.",
            409 when Says("invitee-roster-full") => "That player's friend list is full.",
            500 when Says("max_roster_size_sender") => "Your friend list is full.",
            500 when Says("max_outgoing_invites") => "You have too many pending friend requests.",
            _ => "League couldn't send the friend request. Try again.",
        };
    }

    /// <summary>League's error <c>message</c> and <c>errorCode</c>, without our own "League client returned …" prefix.</summary>
    private static string LeagueText(LcuRequestException ex) => $"{ex.LcuMessage} {ex.ErrorCode}";

    /// <summary>
    /// Reasons League's error texts give often enough to be worth a sentence, matched loosely (lower-case, <c>_</c> and
    /// <c>-</c> read as spaces) because the wording differs between endpoints and patches. Null when nothing matches.
    /// </summary>
    private static string? KnownReason(string text)
    {
        var t = text.ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
        bool Has(params string[] parts) => parts.Any(p => t.Contains(p, StringComparison.Ordinal));
        if (Has("cooldown", "cool down", "too soon", "rate limit", "too many requests"))
            return "Wait a moment and try again.";
        if (Has("gatekeeper", "queue penalty", "leaverbuster", "leaver buster", "queue lockout", "queue dodge", "low priority"))
            return "You can't queue right now.";
        if (Has("not the leader", "not leader", "not party leader", "not lobby leader", "not the owner", "not owner",
                "not party owner", "not lobby owner", "only the owner", "only the leader"))
            return "Only the party leader can do that.";
        if (Has("not owned", "unowned", "doesn't own", "does not own", "not unlocked"))
            return "You don't own that.";
        if (Has("lobby is full", "party is full", "lobby full", "party full"))
            return "The party is full.";
        if (Has("not your turn", "not in progress", "already completed", "already been completed", "action is completed"))
            return "That turn is over.";
        if (Has("disabled", "unavailable", "not available", "not allowed"))
            return "That's not available right now.";
        return null;
    }
}
