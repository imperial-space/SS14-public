using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Австраликус: впрыснутый в золотой экстракт, призывает пауков.</summary>
[RegisterComponent]
public sealed partial class UplinkSpiderInjectorComponent : Component
{
    [DataField]
    public EntProtoId Spider = "ImperialAustralicusSpider";

    [DataField]
    public int Amount = 3;
}
