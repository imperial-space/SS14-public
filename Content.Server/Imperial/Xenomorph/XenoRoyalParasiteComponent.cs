using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>Королевский паразит: делает ксеноморфа преторианцем.</summary>
[RegisterComponent]
public sealed partial class XenoRoyalParasiteComponent : Component
{
    [DataField]
    public EntProtoId Praetorian = "ImperialXenoPraetorian";

    /// <summary>promotion_plasma_cost: списывается при возвышении.</summary>
    [DataField]
    public float Cost = 500;
}
