using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Противотанковая мина: пехота проходит, под гусеницами взрывается.</summary>
[RegisterComponent]
public sealed partial class CadiaTankMineComponent : Component
{
    [DataField]
    public float Intensity = 260f;

    [DataField]
    public float MineSlope = 10f;

    [DataField]
    public float MineMaxTileIntensity = 30f;
}
