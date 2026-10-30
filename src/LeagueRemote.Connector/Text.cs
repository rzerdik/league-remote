namespace LeagueRemote.Connector;

/// <summary>Bounds for text that goes to the phone (names, notes, log lines), so one odd value can't bloat a slice.</summary>
internal static class Text
{
    /// <summary>
    /// At most <paramref name="max"/> UTF-16 characters, never splitting a surrogate pair; "" for null. With
    /// <paramref name="ellipsis"/> a clipped text ends in "…" (counted in <paramref name="max"/>).
    /// </summary>
    public static string Clip(string? text, int max, bool ellipsis = false)
    {
        if (text is null)
            return "";
        if (text.Length <= max)
            return text;

        var keep = ellipsis ? max - 1 : max;
        if (keep > 0 && char.IsHighSurrogate(text[keep - 1]))
            keep--;
        return ellipsis ? text[..keep] + "…" : text[..keep];
    }

    /// <summary>Null for null, empty or whitespace (the LCU sends "" for "no value"), else <paramref name="text"/>.</summary>
    public static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
