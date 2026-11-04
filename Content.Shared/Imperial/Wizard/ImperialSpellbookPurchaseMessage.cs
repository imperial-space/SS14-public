using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

[Serializable, NetSerializable]
public sealed class ImperialSpellbookPurchaseMessage(string entry) : BoundUserInterfaceMessage
{
    public string Entry = entry;
}
