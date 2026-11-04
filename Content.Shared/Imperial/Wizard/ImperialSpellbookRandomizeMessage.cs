using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>semirandomize / randomize.</summary>
[Serializable, NetSerializable]
public sealed class ImperialSpellbookRandomizeMessage(bool full) : BoundUserInterfaceMessage
{
    public bool Full = full;
}
