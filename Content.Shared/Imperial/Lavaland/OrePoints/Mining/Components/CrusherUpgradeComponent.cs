using Robust.Shared.GameStates;
using Robust.Shared.Localization;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Marks an item as a KineticCrusher upgrade.
/// Insert it into a crusher that has <see cref="KineticCrusherUpgradeableComponent"/> to activate.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherUpgradeComponent : Component
{
    /// <summary>How much of the crusher's capacity this upgrade occupies.</summary>
    [DataField]
    public int SlotCost = 25;

    /// <summary>Localisation key shown in the crusher's examine text while this upgrade is installed.</summary>
    [DataField]
    public LocId ExamineText = "";
}
