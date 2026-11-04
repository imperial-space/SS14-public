using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Временные бонусы вокс-команд и воодушевления комиссара.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaSquadBuffComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan SuppressUntil;

    [DataField, AutoNetworkedField]
    public TimeSpan HoldUntil;

    [DataField, AutoNetworkedField]
    public TimeSpan ChargeUntil;
}
