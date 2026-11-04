using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Paradise;

/// <summary>Гаррота из оптоволокна (Paradise, /obj/item/garrote).</summary>
[RegisterComponent]
public sealed partial class UplinkGarroteComponent : Component
{
    /// <summary>Урон удушьем за тик удушения (process раз в ~2 секунды).</summary>
    [DataField]
    public float OxyDamage = 4f;

    [DataField]
    public TimeSpan TickTime = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public EntityUid? Strangling;

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public TimeSpan CooldownUntil;
}
