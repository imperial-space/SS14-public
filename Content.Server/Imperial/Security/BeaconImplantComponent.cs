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

/// <summary>Имплант-маяк (implant/beacon): носитель — точка для телепортации.</summary>
[RegisterComponent]
public sealed partial class BeaconImplantComponent : Component
{
    /// <summary>lifespan_postmortem: 10 минут после смерти.</summary>
    [DataField]
    public TimeSpan LifespanPostmortem = TimeSpan.FromMinutes(10);

    [ViewVariables]
    public TimeSpan? DiedAt;

    /// <summary>Точку телепортации добавил сам имплант (у моба её раньше не было).</summary>
    [ViewVariables]
    public bool AddedWarp;
}
