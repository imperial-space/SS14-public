using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>mouthhole: снимает блокировку еды со шлема.</summary>
[RegisterComponent]
public sealed partial class ImperialModMouthholeComponent : Component
{
    [ViewVariables]
    public EntityUid? Helmet;
}
