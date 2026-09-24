using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>Codex — allows rune drawing and rift absorption when open.</summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(true)]
public sealed partial class HereticCodexComponent : Component
{
    [AutoNetworkedField]
    public bool IsOpen = false;

    [DataField]
    public float DrawTimeFast = 8.2f;

    [DataField]
    public string DrawEffectEntity = "HereticEffectRuneDrawFast";

    /// <summary>Если true, ЛКМ на руну накладывает проклятие вместо стирания.</summary>
    [DataField]
    public bool CanCurseRunes = false;

    /// <summary>Если true, изучение открытого кодекса вызывает галлюцинации у не-еретиков.</summary>
    [DataField]
    public bool CausesHallucinations = false;

    // ─── Sprite states ───────────────────────────────────────────────────────

    [DataField]
    public string SpriteStateClosed = "book";

    [DataField]
    public string SpriteStateOpening = "book_opening";

    [DataField]
    public string SpriteStateOpen = "book_open";

    [DataField]
    public string SpriteStateClosing = "book_closing";
}
