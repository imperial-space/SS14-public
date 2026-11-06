using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

[Serializable, NetSerializable]
public sealed class ImperialSpellbookRefundMessage(string entry) : BoundUserInterfaceMessage
{
    public string Entry = entry;
}
