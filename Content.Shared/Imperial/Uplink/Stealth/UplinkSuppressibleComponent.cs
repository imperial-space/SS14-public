using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Оружие, на которое можно навинтить глушитель (can_suppress).</summary>
[RegisterComponent]
public sealed partial class UplinkSuppressibleComponent : Component
{
    [DataField]
    public string Slot = "suppressor";

    [ViewVariables]
    public Robust.Shared.Audio.SoundSpecifier? OriginalSound;
}
