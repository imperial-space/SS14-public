using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Медвежий капкан (restraints/legcuffs/beartrap): взводится в руке, срабатывает при наступании.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialBearTrapComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Armed;

    [DataField]
    public float Damage = 20f;

    [DataField]
    public float ArmTime = 2f;
}
