using System.Numerics;
using Content.Server.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared.Chasm;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

public sealed class WormholeGeneratorSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChasmFallingComponent, ComponentInit>(OnChasmFallingInit);
        SubscribeLocalEvent<WormholeStasisComponent, UpdateCanMoveEvent>(OnStasisUpdateCanMove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WormholeStasisComponent>();
        while (query.MoveNext(out var uid, out var stasis))
        {
            if (_timing.CurTime < stasis.ReleaseTime)
                continue;

            RemComp<WormholeStasisComponent>(uid);
            _blocker.UpdateCanMove(uid);
        }
    }

    private void OnStasisUpdateCanMove(EntityUid uid, WormholeStasisComponent component, UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnChasmFallingInit(EntityUid uid, ChasmFallingComponent component, ComponentInit args)
    {
        if (!TryFindWormholeGenerator(uid, out var genUid))
            return;

        SaveFromChasm(uid, genUid);
    }

    /// <summary>
    /// Recursively searches entity's containers for a WormholeGeneratorComponent.
    /// Handles nested containers (e.g., item in box in backpack).
    /// </summary>
    private bool TryFindWormholeGenerator(EntityUid entity, out EntityUid gen)
    {
        if (!TryComp<ContainerManagerComponent>(entity, out var containerManager))
        {
            gen = default;
            return false;
        }

        foreach (var container in containerManager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (HasComp<WormholeGeneratorComponent>(contained))
                {
                    gen = contained;
                    return true;
                }

                // Recurse into nested containers (box in backpack, etc.)
                if (TryFindWormholeGenerator(contained, out gen))
                    return true;
            }
        }

        gen = default;
        return false;
    }

    private void SaveFromChasm(EntityUid uid, EntityUid genUid)
    {
        var xform = Transform(uid);
        var teleported = false;

        // Find a safe tile farther from the chasm edge.
        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            var playerTile = _mapSystem.LocalToTile(gridUid, grid, xform.Coordinates);

            for (var radius = 3; radius <= 8 && !teleported; radius++)
            {
                for (var dx = -radius; dx <= radius && !teleported; dx++)
                {
                    for (var dy = -radius; dy <= radius && !teleported; dy++)
                    {
                        // Only check outermost ring
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                            continue;

                        var checkTile = new Vector2i(playerTile.X + dx, playerTile.Y + dy);
                        var tileRef = _mapSystem.GetTileRef((gridUid, grid), checkTile);

                        if (tileRef.Tile.IsEmpty)
                            continue;

                        if (IsTileNearChasm(gridUid, grid, checkTile, radius: 2))
                            continue;

                        var safeCoords = new EntityCoordinates(
                            gridUid,
                            new Vector2(checkTile.X + 0.5f, checkTile.Y + 0.5f));
                        _transform.SetCoordinates(uid, safeCoords);
                        teleported = true;
                    }
                }
            }
        }

        // Remove the falling component — cancels deletion and re-enables movement
        RemComp<ChasmFallingComponent>(uid);

        if (teleported)
        {
            var stasis = EnsureComp<WormholeStasisComponent>(uid);
            stasis.ReleaseTime = _timing.CurTime + TimeSpan.FromSeconds(1);
        }

        _blocker.UpdateCanMove(uid);

        // Consume the wormhole generator
        QueueDel(genUid);

        _popup.PopupEntity(Loc.GetString("wormhole-generator-saved"), uid, uid, PopupType.LargeCaution);
    }

    private bool IsTileNearChasm(EntityUid gridUid, MapGridComponent grid, Vector2i center, int radius)
    {
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                var tile = new Vector2i(center.X + dx, center.Y + dy);
                foreach (var anchoredEnt in _mapSystem.GetAnchoredEntities(gridUid, grid, tile))
                {
                    if (HasComp<ChasmComponent>(anchoredEnt))
                        return true;
                }
            }
        }

        return false;
    }
}

[RegisterComponent]
public sealed partial class WormholeStasisComponent : Component
{
    public TimeSpan ReleaseTime;
}
