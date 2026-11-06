using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Действие подготовленного заклинания крови (datum/action/innate/cult/blood_spell).</summary>
[RegisterComponent]
public sealed partial class CultBloodSpellComponent : Component
{
    [DataField(required: true)]
    public CultSpell Spell;

    /// <summary>charges.</summary>
    [DataField]
    public int Charges = 1;

    /// <summary>health_cost: brute в руку за применение.</summary>
    [DataField]
    public float HealthCost;

    /// <summary>deletes_on_empty.</summary>
    [DataField]
    public bool DeletesOnEmpty = true;

    /// <summary>magic_path: прототип ауры в руке; null — мгновенное заклинание.</summary>
    [DataField]
    public EntProtoId? Aura;

    [DataField]
    public string? Invocation;

    [DataField]
    public string BaseDescription = string.Empty;

    [ViewVariables] public EntityUid? OwnerEntity;
    [ViewVariables] public EntityUid? Hand;

    /// <summary>Conceal Presence: следующее применение раскроет.</summary>
    [ViewVariables] public bool Revealing;

    [DataField]
    public float CastVeilingRange = 5f;

    [DataField]
    public float CastVeilingRange2 = 7f;
}
