using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

/// <summary>Строка панели (ui_static_data TG): эмоция доступна этому существу.</summary>
[Serializable, NetSerializable]
public sealed class ImperialEmoteEntry
{
    public string Id = string.Empty;
    public bool Hands;
    public bool Visible;
    public bool Audible;
    public bool Sound;
    public bool Param;
    public bool Action;
}
