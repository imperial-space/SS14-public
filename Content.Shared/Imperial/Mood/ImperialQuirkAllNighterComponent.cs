using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Mood;

/// <summary>/datum/quirk/all_nighter: «копилка сна» и усталость без стимуляторов.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkAllNighterComponent : Component
{
    [DataField]
    public List<string> Stimulants = new();

    [ViewVariables]
    public float SleepBank;
}
