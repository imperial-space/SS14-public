using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Зловещий осколок (cyberimp/arm/toolkit/shard/scythe из SS13): при использовании в руке уходит в руку
/// владельца и позволяет призывать порочную косу.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NullRodScytheShardComponent : Component
{
    [DataField]
    public EntProtoId Scythe = "NullRodVorpalScythe";

    [DataField]
    public SoundSpecifier? ImplantSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");
}
