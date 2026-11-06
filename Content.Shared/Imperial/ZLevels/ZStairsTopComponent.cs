using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>Метка площадки над лестницей: зашёл на неё со стороны этажа, идя вниз, — оказался на лестнице.</summary>
[RegisterComponent]
public sealed partial class ZStairsTopComponent : Component
{
    [ViewVariables]
    public EntityUid? Stairs;
}
