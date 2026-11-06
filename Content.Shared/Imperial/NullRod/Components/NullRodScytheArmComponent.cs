using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Осколок в руке святого: действие призывает и убирает косу.
/// </summary>
[RegisterComponent]
public sealed partial class NullRodScytheArmComponent : Component
{
    [DataField]
    public EntProtoId Scythe = "NullRodVorpalScythe";

    [DataField]
    public EntProtoId Action = "ActionNullRodToggleScythe";

    [ViewVariables]
    public EntityUid? ActionEntity;

    [ViewVariables]
    public EntityUid? ScytheEntity;

    /// <summary>Урон, если убрать косу, не дав ей вкусить крови.</summary>
    [DataField]
    public float RetractDamage = 25f;

    [DataField]
    public SoundSpecifier? ExtendSound = new SoundPathSpecifier("/Audio/Items/unsheath.ogg");

    [DataField]
    public SoundSpecifier? RetractSound = new SoundPathSpecifier("/Audio/Items/sheath.ogg");
}
