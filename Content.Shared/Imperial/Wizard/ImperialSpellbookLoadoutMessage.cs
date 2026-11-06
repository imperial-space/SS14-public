using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>purchase_loadout.</summary>
[Serializable, NetSerializable]
public sealed class ImperialSpellbookLoadoutMessage(string loadout) : BoundUserInterfaceMessage
{
    public string Loadout = loadout;
}
