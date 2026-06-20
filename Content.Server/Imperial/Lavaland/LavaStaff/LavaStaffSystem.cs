using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Imperial.Lavaland.LavaStaff;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.LavaStaff;

public sealed class LavaStaffSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ITileDefinitionManager _tiledef = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly GunSystem _gunSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeLocalEvent<LavaStaffComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, LavaStaffComponent component, AfterInteractEvent args)
    {
        if (args.Handled || args.Target != null)
            return;

        var now = _timing.CurTime;
        if (now - component.LastTileChange < component.TileCooldown)
            return;

        component.LastTileChange = now;

        var targetCoords = args.ClickLocation;
        var mapCoords = _xform.ToMapCoordinates(targetCoords);

        // Check for existing FloorLavaEntity at target
        var lavaEntities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(mapCoords.MapId, mapCoords.Position, 0.5f, lavaEntities);

        EntityUid? lavaEnt = null;
        foreach (var ent in lavaEntities)
        {
            if (MetaData(ent).EntityPrototype?.ID == component.LavaEntityPrototype)
            {
                lavaEnt = ent;
                break;
            }
        }

        if (lavaEnt.HasValue)
        {
            // Remove lava → revert to basalt
            QueueDel(lavaEnt.Value);

            // Set tile to basalt if on a grid
            if (_mapManager.TryFindGridAt(mapCoords, out var gridUid, out var gridComp))
            {
                if (_tiledef.TryGetDefinition(component.BasaltTileId, out var basaltDef) &&
                    _mapSystem.TryGetTileRef(gridUid, gridComp, targetCoords, out var tileRef))
                {
                    _tile.ReplaceTile(tileRef, (ContentTileDefinition) basaltDef);
                }
            }
        }
        else
        {
            // Spawn lava entity
            Spawn(component.LavaEntityPrototype, targetCoords);
        }

        // Fire projectile toward target
        FireProjectile(uid, component, args.User, targetCoords);
        args.Handled = true;
    }

    private void FireProjectile(EntityUid staffUid, LavaStaffComponent component,
        EntityUid user, EntityCoordinates targetCoords)
    {
        var userXform = _xformQuery.GetComponent(user);
        var userPos = _xform.GetWorldPosition(userXform);
        var targetPos = _xform.ToMapCoordinates(targetCoords).Position;
        var direction = targetPos - userPos;

        if (direction == Vector2.Zero)
            return;

        var mapPos = _xform.ToMapCoordinates(Transform(user).Coordinates);
        var spawnCoords = _mapManager.TryFindGridAt(mapPos, out var gridUid, out _)
            ? _xform.WithEntityId(Transform(user).Coordinates, gridUid)
            : new EntityCoordinates(_mapSystem.GetMapOrInvalid(mapPos.MapId), mapPos.Position);

        var projectile = Spawn(component.ProjectilePrototype, spawnCoords);
        _gunSystem.ShootProjectile(projectile, direction, Vector2.Zero, user, staffUid, component.ProjectileSpeed);
    }
}
