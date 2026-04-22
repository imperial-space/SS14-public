using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Fishing.FishingRodComponents;

[RegisterComponent]
public sealed partial class FishingRodUpgraderComponent : Component
{
    [DataField]
    public float ReelCoefficent = 1f;
    [DataField]
    public float HookCoefficent = 1f;
}

