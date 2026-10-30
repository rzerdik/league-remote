namespace LeagueRemote.Connector.Tests;

/// <summary>
/// Champ-select skin samples. <b>Composed, not recorded</b>: champ select wasn't running when the endpoint was verified
/// (Oct 2026, patch 16.21), so the shape follows the client's <c>/help</c> (<c>GetLolChampSelectV1SkinCarouselSkins</c> →
/// <c>TeamBuilderDirect-SkinSelectorSkin[]</c>, <c>childSkins</c> → <c>TeamBuilderDirect-SkinSelectorChildSkin</c>) and the
/// fields rcp-fe-lol-champ-select reads. Replace with a recorded (anonymised) carousel once one is seen live.
/// </summary>
internal static class SkinSamples
{
    /// <summary>
    /// <c>/lol-champ-select/v1/skin-carousel-skins</c> for Ahri (103). Owned: the default skin, Dynasty (103001) and Star
    /// Guardian (103015) with one owned chroma (103016), one unowned (103017) and one owned but disabled (103018), plus a
    /// tiered form without colours (103019). Not equippable: Arcade (103014, not owned), a disabled skin (103020) and a
    /// stale Lux skin (99001). One colour isn't a hex colour and must never reach the phone.
    /// </summary>
    public const string AhriCarousel = """
    [
      { "id": 103001, "championId": 103, "name": "Dynasty Ahri", "isBase": false, "disabled": false, "unlocked": true,
        "ownership": { "owned": true, "rental": { "rented": false } }, "childSkins": [] },
      { "id": 103000, "championId": 103, "name": "Ahri", "isBase": true, "disabled": false, "unlocked": true,
        "ownership": { "owned": true, "rental": { "rented": false } }, "childSkins": [] },
      { "id": 103014, "championId": 103, "name": "Arcade Ahri", "isBase": false, "disabled": false, "unlocked": false,
        "ownership": { "owned": false, "rental": { "rented": false } }, "childSkins": [] },
      { "id": 103015, "championId": 103, "name": "Star Guardian Ahri", "isBase": false, "disabled": false, "unlocked": true,
        "ownership": { "owned": true, "rental": { "rented": false } }, "chromaPreviewPath": "/lol-game-data/assets/v1/chroma.png",
        "childSkins": [
          { "id": 103016, "championId": 103, "parentSkinId": 103015, "name": "Star Guardian Ahri (Rose Quartz)", "shortName": "Rose Quartz",
            "colors": [ "#f0a6de", "#5BB9E8" ], "disabled": false, "unlocked": true, "ownership": { "owned": true } },
          { "id": 103017, "championId": 103, "parentSkinId": 103015, "name": "Star Guardian Ahri (Obsidian)", "shortName": "Obsidian",
            "colors": [ "#1C1C24" ], "disabled": false, "unlocked": false, "ownership": { "owned": false } },
          { "id": 103018, "championId": 103, "parentSkinId": 103015, "name": "Star Guardian Ahri (Event)", "shortName": "Event",
            "colors": [ "#FFD700" ], "disabled": true, "unlocked": true, "ownership": { "owned": true } },
          { "id": 103019, "championId": 103, "parentSkinId": 103015, "name": "", "shortName": "Form 2", "stage": 2,
            "colors": [ "red;background:url(x)" ], "disabled": false, "unlocked": true, "ownership": { "owned": true } }
        ] },
      { "id": 103020, "championId": 103, "name": "Retired Ahri", "isBase": false, "disabled": true, "unlocked": true,
        "ownership": { "owned": true }, "childSkins": [] },
      { "id": 99001, "championId": 99, "name": "Sorceress Lux", "isBase": false, "disabled": false, "unlocked": true,
        "ownership": { "owned": true }, "childSkins": [] }
    ]
    """;

    /// <summary>
    /// Trimmed champ-select session in finalization: the local player (cell 2) locked in Ahri and wears Dynasty Ahri
    /// (<c>selectedSkinId</c> on their <c>myTeam</c> entry, <c>TeamBuilderDirect-ChampSelectPlayerSelection</c>).
    /// </summary>
    public const string LockedSession = """
    {
      "localPlayerCellId": 2,
      "allowSkinSelection": true,
      "timer": { "phase": "FINALIZATION", "adjustedTimeLeftInPhase": 25000 },
      "myTeam": [
        { "cellId": 1, "championId": 64, "selectedSkinId": 64007, "assignedPosition": "jungle", "spell1Id": 4, "spell2Id": 11 },
        { "cellId": 2, "championId": 103, "selectedSkinId": 103001, "assignedPosition": "middle", "spell1Id": 4, "spell2Id": 14 }
      ],
      "theirTeam": [],
      "actions": [
        [
          { "id": 20, "actorCellId": 1, "championId": 64, "type": "pick", "completed": true, "isAllyAction": true, "isInProgress": false },
          { "id": 21, "actorCellId": 2, "championId": 103, "type": "pick", "completed": true, "isAllyAction": true, "isInProgress": false }
        ]
      ]
    }
    """;
}
