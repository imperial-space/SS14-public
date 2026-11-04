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

/// <summary>Имплант запрета телепортации (implant/teleport_blocker).</summary>
[RegisterComponent]
public sealed partial class TeleportBlockerImplantComponent : Component;
