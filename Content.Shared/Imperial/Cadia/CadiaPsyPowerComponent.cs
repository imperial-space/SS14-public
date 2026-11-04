using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Сила на сущности-действии: что делает, сколько заряда стоит и сколько напряжения даёт.</summary>
[RegisterComponent]
public sealed partial class CadiaPsyPowerComponent : Component
{
    [DataField(required: true)]
    public CadiaPsyPower Power;

    [DataField]
    public float Cost = 20f;

    [DataField]
    public float Strain = 15f;
}
