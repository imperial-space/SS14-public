using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Server.Imperial.Lavaland.MegafaunaTracker;
using Content.Server.Imperial.Lavaland.Resonator;
using Content.Server.Imperial.Lavaland.Storm;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>status_effect/syphon_mark.</summary>
[RegisterComponent]
public sealed partial class KineticSyphonMarkComponent : Component
{
    [ViewVariables]
    public EntityUid Modkit;

    [ViewVariables]
    public TimeSpan ExpiresAt;
}
