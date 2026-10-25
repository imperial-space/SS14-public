using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

/// <summary>Открыть панель эмоций (EmotePanel TG + custom_emote_panel BandaStation). Действие только клиентское.</summary>
public sealed partial class ImperialEmotePanelActionEvent : InstantActionEvent;

/// <summary>
/// Своя эмоция из панели: только текст (TGUI_PANEL_EMOTE_TYPE_ME)
/// или текст поверх стандартной эмоции со звуком (TGUI_PANEL_EMOTE_TYPE_CUSTOM).
/// </summary>
[Serializable, NetSerializable]
public sealed class ImperialCustomEmoteMessage(ProtoId<ImperialEmotePrototype>? emote, string text) : EntityEventArgs
{
    public readonly ProtoId<ImperialEmotePrototype>? Emote = emote;
    public readonly string Text = text;
}

public static class ImperialEmotePanel
{
    /// <summary>TGUI_PANEL_MAX_EMOTES.</summary>
    public const int MaxEmotes = 45;

    /// <summary>TGUI_PANEL_MAX_EMOTE_LENGTH.</summary>
    public const int MaxTextLength = 128;

    /// <summary>TGUI_PANEL_MAX_EMOTE_NAME_LENGTH.</summary>
    public const int MaxNameLength = 32;
}
