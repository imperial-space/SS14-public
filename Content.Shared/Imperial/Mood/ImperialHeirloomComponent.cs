using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Mood;

/// <summary>Семейная реликвия: чья она.</summary>
[RegisterComponent]
public sealed partial class ImperialHeirloomComponent : Component
{
    [ViewVariables]
    public EntityUid OwnerEntity;
    [ViewVariables]
    public string Family = string.Empty;
}
