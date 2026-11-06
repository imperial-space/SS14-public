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

    [DataField]
    public TimeSpan CastStunTime = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan CastStunDuration = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan CastShacklesDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan CastConstructionDelay = TimeSpan.FromSeconds(9);

    [DataField]
    public TimeSpan CastConstructionDelay2 = TimeSpan.FromSeconds(5);

    [DataField]
    public float CastStunRange = 1.1f;

    [DataField]
    public TimeSpan CastStunSeconds = TimeSpan.FromSeconds(0.2f);

    [DataField]
    public float CastStunRange2 = 1.5f;

    [DataField]
    public float CastStunSeconds2 = 0.5f;

    [DataField]
    public float CastStunRange3 = 0.5f;

    [DataField]
    public float CastStunEnergyConsumption = 100000f;

    [DataField]
    public TimeSpan HealCultistSeconds = TimeSpan.FromSeconds(1.5f);

    [DataField]
    public float BloodDrawRange = 2.5f;

    [DataField]
    public TimeSpan BloodDrawSeconds = TimeSpan.FromSeconds(1.5f);
}
