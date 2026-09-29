using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Actions;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Decals;
using Content.Server.Doors.Systems;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Antag;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Decals;
using Content.Shared.Doors.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Follower.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Imperial.Heretic.Paths.Rust;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Overlays;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Speech.Muting;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using CancellationTokenSource = System.Threading.CancellationTokenSource;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Ржавые тайлы.
/// </summary>
public sealed partial class HereticSystem
{
    public bool IsTileRusted(EntityCoordinates coordinates)
    {
        var gridUid = _xform.GetGrid(coordinates);
        if (gridUid is not { } grid) return false;
        var snapped = coordinates.SnapToGrid(EntityManager);
        foreach (var (_, decal) in _decal.GetDecalsInRange(grid, snapped.Position))
        {
            if (decal.Id == RustDecalId) return true;
        }
        var mapCoords = _xform.ToMapCoordinates(snapped);
        return _lookup.GetEntitiesInRange<HereticRustOverlayComponent>(mapCoords, 0.4f).Count > 0;
    }

    public void RustTile(EntityCoordinates coordinates)
    {
        if (IsTileRusted(coordinates)) return;
        var snapped = coordinates.SnapToGrid(EntityManager);
        _decal.TryAddDecal(RustDecalId, snapped, out _);
    }

    public void UnrustTile(EntityCoordinates coordinates)
    {
        var gridUid = _xform.GetGrid(coordinates);
        if (gridUid is not { } grid) return;
        var snapped = coordinates.SnapToGrid(EntityManager);
        foreach (var (decalId, decal) in _decal.GetDecalsInRange(grid, snapped.Position))
        {
            if (decal.Id == RustDecalId)
                _decal.RemoveDecal(grid, decalId);
        }
    }

    /// <summary>
    /// Spreads rust to every tile in range, and destroys any HereticRustWall entities in range.
    /// </summary>
    public void SpreadRustNearby(EntityCoordinates origin, float radius)
    {
        var gridUid = _xform.GetGrid(origin);
        if (gridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var worldPos = _xform.ToMapCoordinates(origin).Position;
        var circle = new Circle(worldPos, radius);
        foreach (var tile in _mapSystem.GetTilesIntersecting(grid, gridComp, circle))
        {
            RustTile(_mapSystem.GridTileToLocal(grid, gridComp, tile.GridIndices));
        }

        foreach (var wall in _lookup.GetEntitiesInRange(origin, radius).ToList())
        {
            var protoId = MetaData(wall).EntityPrototype?.ID;
            if (protoId == "HereticRustWall")
            {
                QueueDel(wall);
            }
            else if (protoId is "WallSolid" or "WallReinforced")
            {
                var wallCoords = Transform(wall).Coordinates;
                QueueDel(wall);
                Spawn(protoId == "WallSolid" ? "WallSolidRust" : "WallReinforcedRust", wallCoords);
            }
        }
    }

    // SS13-identical wave: rust covers the whole station tile by tile, ring by ring (Chebyshev).
    // Ring d fires after 2*d seconds; each ring is shuffled and split into thirds staggered over 5 seconds.
    public void TriggerRustAscensionWave(EntityCoordinates origin)
    {
        var gridUid = _xform.GetGrid(origin);
        if (gridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var worldOrigin = _xform.ToMapCoordinates(origin).Position;
        var byDistance  = new Dictionary<int, List<Vector2i>>();

        foreach (var tile in _mapSystem.GetAllTiles(grid, gridComp))
        {
            var tileCoords = _mapSystem.GridTileToLocal(grid, gridComp, tile.GridIndices);
            var tileWorld  = _xform.ToMapCoordinates(tileCoords).Position;
            var dx   = (int)MathF.Round(MathF.Abs(tileWorld.X - worldOrigin.X));
            var dy   = (int)MathF.Round(MathF.Abs(tileWorld.Y - worldOrigin.Y));
            var dist = Math.Max(dx, dy);

            if (!byDistance.TryGetValue(dist, out var list))
            {
                list = new List<Vector2i>();
                byDistance[dist] = list;
            }
            list.Add(tile.GridIndices);
        }

        foreach (var (dist, indices) in byDistance)
        {
            var ringDelay     = 2000 * dist;
            var capturedGrid  = grid;
            var capturedTiles = new List<Vector2i>(indices);

            Timer.Spawn(ringDelay, () =>
            {
                if (!Exists(capturedGrid) || !TryComp<MapGridComponent>(capturedGrid, out var gc)) return;

                _random.Shuffle(capturedTiles);
                var count = capturedTiles.Count;
                var third = Math.Max(1, count / 3);

                for (var i = 0; i < count; i++)
                {
                    var idx       = capturedTiles[i];
                    var staggerMs = i < third       ? 1650
                                  : i < third * 2   ? 3300
                                                    : 5000;

                    Timer.Spawn(staggerMs, () =>
                    {
                        if (!Exists(capturedGrid) || !TryComp<MapGridComponent>(capturedGrid, out var gcInner)) return;
                        var coords  = _mapSystem.GridTileToLocal(capturedGrid, gcInner, idx);
                        RustTile(coords);
                        Spawn("HereticRustOverlay", coords);
                        foreach (var wall in _lookup.GetEntitiesInRange(coords, 0.6f).ToList())
                        {
                            if (TerminatingOrDeleted(wall)) continue;
                            var protoId = MetaData(wall).EntityPrototype?.ID;
                            if (protoId is not ("WallSolid" or "WallReinforced")) continue;
                            var wallPos = Transform(wall).Coordinates;
                            QueueDel(wall);
                            Spawn(protoId == "WallSolid" ? "WallSolidRust" : "WallReinforcedRust", wallPos);
                        }
                        var offsetX = (_random.NextFloat() * 2f - 1f) * 0.1875f;
                        var offsetY = (_random.NextFloat() * 2f - 1f) * 0.1875f;
                        var runeId  = RustAscensionRuneEffects[_random.Next(RustAscensionRuneEffects.Length)];
                        Spawn(runeId, new EntityCoordinates(coords.EntityId, coords.Position + new Vector2(offsetX, offsetY)));
                    });
                }
            });
        }
    }

}
