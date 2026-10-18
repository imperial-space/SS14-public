using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.Clothing;

/// <summary>
/// datum/component/armor_plate: шкуры голиафа, нашитые на одежду, усиливают защиту от ударов.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ArmorPlateComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Amount;

    /// <summary>maxamount.</summary>
    [DataField]
    public int MaxAmount = 3;

    /// <summary>upgrade_item: тип стака.</summary>
    [DataField]
    public ProtoId<StackPrototype> UpgradeStack = "GoliathHide";

    /// <summary>Броня melee без пластин, проценты.</summary>
    [DataField]
    public float BaseMelee = 30f;

    /// <summary>armor_plate melee за одну пластину, проценты.</summary>
    [DataField]
    public float MeleePerPlate = 10f;
}
