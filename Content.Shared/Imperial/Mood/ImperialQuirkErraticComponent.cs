using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Mood;

/// <summary>/datum/quirk/erratic: личность то и дело меняется.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkErraticComponent : Component
{
    [ViewVariables]
    public List<string> BasePersonalities = new();
    [ViewVariables]
    public TimeSpan NextChange;
    [ViewVariables]
    public int Index;
    [ViewVariables]
    public bool Initialized;
}
