using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic.Components;

/// <summary>
/// Огненная клятва: сущность оставляет за собой огненный след и обжигает всех рядом.
/// </summary>
[RegisterComponent]
public sealed partial class HereticFlameOathComponent : Component
{
    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(0.2);

    [DataField]
    public float Radius = 1.5f;

    [DataField]
    public float FireStacks = 0.5f;

    [DataField]
    public DamageSpecifier Damage = new() { DamageDict = { ["Heat"] = FixedPoint2.New(0.5f) } };

    [DataField]
    public EntProtoId FireEffect = "HereticAshSpiritFire";

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public int TicksLeft;

    /// <summary>Зацикленный звук огня, пока клятва активна.</summary>
    [ViewVariables]
    public EntityUid? Audio;
}
