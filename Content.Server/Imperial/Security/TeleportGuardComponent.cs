using Content.Server.Atmos.EntitySystems;
using Content.Server.Imperial.Lavaland.LavalandShuttle;
using Content.Server.Station.Systems;
using Content.Shared.Atmos;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.UserInterface;
using Content.Shared.Warps;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>Метка на носителе: следит за мгновенными перемещениями.</summary>
[RegisterComponent]
public sealed partial class TeleportGuardComponent : Component
{
    [ViewVariables]
    public int Exile;

    [ViewVariables]
    public int Blockers;

    /// <summary>Куда вернуть после пресечённого телепорта.</summary>
    [ViewVariables]
    public MapCoordinates? RevertTo;

    [ViewVariables]
    public bool RevertStun;

    [ViewVariables]
    public bool Reverting;
}
