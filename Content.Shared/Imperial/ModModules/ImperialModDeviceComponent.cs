using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>Устройство, выданное активным модулем в руку (device). Возвращается в модуль при выключении.</summary>
[RegisterComponent]
public sealed partial class ImperialModDeviceComponent : Component
{
    [ViewVariables]
    public EntityUid Module;
}
