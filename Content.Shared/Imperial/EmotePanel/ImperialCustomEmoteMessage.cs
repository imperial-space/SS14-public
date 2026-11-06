using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

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
