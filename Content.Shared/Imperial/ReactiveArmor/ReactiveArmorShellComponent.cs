using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ReactiveArmor;

/// <summary>reactive_armor_shell: оболочка, которая ждёт ядро аномалии.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ReactiveArmorShellComponent : Component
{
    /// <summary>anomaly_armour_types: какое ядро во что превращает оболочку.</summary>
    [DataField]
    public Dictionary<EntProtoId, EntProtoId> Armors = new();
}
