using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Помада Синдиката (lipstick/syndie): после нанесения воздушные поцелуи становятся лазерными.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndieLipstickComponent : Component
{
    [DataField]
    public EntProtoId Kisser = "ImperialSyndieKissBlower";
}
