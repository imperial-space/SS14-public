using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Аура в руке (obj/item/melee/blood_magic).</summary>
[RegisterComponent]
public sealed partial class CultAuraComponent : Component
{
    [DataField(required: true)]
    public CultSpell Spell;

    [DataField]
    public string? Invocation;

    [ViewVariables] public int Uses = 1;
    [ViewVariables] public float HealthCost;
    [ViewVariables] public EntityUid? Source;
    [ViewVariables] public bool Channeling;
}
