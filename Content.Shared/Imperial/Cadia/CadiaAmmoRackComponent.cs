using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Боеукладка: выдаёт снаряды по одному, принимает обратно.</summary>
[RegisterComponent]
public sealed partial class CadiaAmmoRackComponent : Component
{
    [ViewVariables]
    public EntityUid? Vehicle;
}
