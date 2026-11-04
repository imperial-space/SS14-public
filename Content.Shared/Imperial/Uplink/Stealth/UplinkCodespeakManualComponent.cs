using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Мануал кодовой речи.</summary>
[RegisterComponent]
public sealed partial class UplinkCodespeakManualComponent : Component
{
    /// <summary>Сколько раз можно научить; -1 — бесконечно (deluxe).</summary>
    [DataField]
    public int Charges = -1;
}
