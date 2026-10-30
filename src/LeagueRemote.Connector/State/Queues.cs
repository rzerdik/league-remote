using System.Text.Json;

namespace LeagueRemote.Connector.State;

/// <summary>Lookups in the client's queue list (<c>/lol-game-queues/v1/queues</c>).</summary>
internal static class Queues
{
    /// <summary>"Multiplayer Practice Tool Custom" in the client's queue list (live since the custom-games-as-queues change).</summary>
    public const int DefaultPracticeToolQueueId = 3140;

    /// <summary>Ids of the queues the client currently offers (<c>queueAvailability</c> "Available"); null without a list.</summary>
    public static IReadOnlyList<int>? AvailableIds(JsonElement? queues) =>
        queues is { ValueKind: JsonValueKind.Array }
            ? queues.Objects()
                .Where(q => q.Str("queueAvailability") == "Available" && q.Int("id") > 0)
                .Select(q => q.Int("id"))
                .Order()
                .ToList()
            : null;

    /// <summary>The queue's name as the client's queue list has it (<c>description</c>: "Ranked Flex", "Draft Pick", ...).</summary>
    public static string? Name(JsonElement? queues, int queueId)
    {
        if (queueId <= 0)
            return null;
        return queues.Objects().FirstOrDefault(q => q.Int("id") == queueId) is { ValueKind: JsonValueKind.Object } queue
            ? Text.NullIfBlank(queue.Str("description")) ?? Text.NullIfBlank(queue.Str("name"))
            : null;
    }

    /// <summary>
    /// Id of the Practice Tool queue (game mode PRACTICETOOL, an available one first), or null when the list is missing or
    /// has none.
    /// </summary>
    public static int? PracticeToolId(JsonElement? queues) =>
        queues.Objects()
            .Where(q => q.Str("gameMode") == "PRACTICETOOL" && q.Int("id") > 0)
            .OrderByDescending(q => q.Str("queueAvailability") == "Available")
            .Select(q => (int?)q.Int("id"))
            .FirstOrDefault();
}
