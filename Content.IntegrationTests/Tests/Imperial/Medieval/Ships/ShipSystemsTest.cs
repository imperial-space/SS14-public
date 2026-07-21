using System.Numerics;
using System.Reflection;
using Content.Server.Imperial.Medieval.Ships.Helm;
using Content.Server.Imperial.Medieval.Ships.PlayerDrowning;
using Content.Server.Imperial.Medieval.Ships.Sail;
using Content.Server.Imperial.Medieval.Ships.Wave;
using Content.Server.Shuttles.Components;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Content.Shared.Imperial.Medieval.Ships.Helm;
using Content.Shared.Imperial.Medieval.Ships.Hull;
using Content.Shared.Imperial.Medieval.Ships.Repairing;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Maps;
using Content.Shared.Stacks;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.Ships;

[TestFixture]
public sealed class ShipSystemsTest
{
    private static ushort GetTileId(ITileDefinitionManager tileDefinitions, string id)
    {
        Assert.That(tileDefinitions.TryGetDefinition(id, out var tile), Is.True, $"Missing tile definition '{id}'.");
        return tile.TileId;
    }

    private static EntityUid SpawnSingleTileGrid(
        IMapManager mapManager,
        SharedMapSystem mapSystem,
        MapId mapId,
        ushort tileId,
        out MapGridComponent gridComp)
    {
        var grid = mapManager.CreateGridEntity(mapId);
        gridComp = grid.Comp;
        mapSystem.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(tileId));
        return grid.Owner;
    }

    private static TEvent CreateCompletedDoAfter<TEvent>(
        IEntityManager entMan,
        EntityUid user,
        EntityUid eventTarget,
        EntityUid? target,
        EntityUid? used,
        TEvent ev) where TEvent : DoAfterEvent
    {
        ev.DoAfter = new Content.Shared.DoAfter.DoAfter(1, new DoAfterArgs(entMan, user, TimeSpan.Zero, ev, eventTarget, target, used), TimeSpan.Zero);

        return ev;
    }

    private static EntityUid FindAnchorOnGrid(IEntityManager entMan, EntityUid gridUid, bool enabled)
    {
        var query = entMan.AllEntityQueryEnumerator<MedievalAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var xform))
        {
            if (xform.ParentUid != gridUid || anchor.Enabled != enabled)
                continue;

            return uid;
        }

        Assert.Fail($"Could not find anchor with Enabled={enabled} on grid {gridUid}.");
        return EntityUid.Invalid;
    }

    [Test]
    public async Task HullStagesFollowExpectedProgression()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefinitions = server.ResolveDependency<ITileDefinitionManager>();
        var shipHull = entMan.System<SharedShipHullSystem>();

        await server.WaitAssertion(() =>
        {
            var intact = shipHull.IntactHullTileId;
            var broken1 = GetTileId(tileDefinitions, "woodbroken1");
            var broken2 = GetTileId(tileDefinitions, "woodbroken2");
            var broken3 = GetTileId(tileDefinitions, "woodbroken3");

            Assert.Multiple(() =>
            {
                Assert.That(shipHull.MaxDamageStage, Is.EqualTo(3));

                Assert.That(shipHull.TryGetDamageStage(intact, out var intactStage), Is.True);
                Assert.That(intactStage, Is.EqualTo(0));

                Assert.That(shipHull.TryGetDamageStage(broken1, out var brokenStage1), Is.True);
                Assert.That(brokenStage1, Is.EqualTo(1));

                Assert.That(shipHull.TryGetDamageStage(broken2, out var brokenStage2), Is.True);
                Assert.That(brokenStage2, Is.EqualTo(2));

                Assert.That(shipHull.TryGetDamageStage(broken3, out var brokenStage3), Is.True);
                Assert.That(brokenStage3, Is.EqualTo(3));

                Assert.That(shipHull.TryGetNextDamageTile(intact, out var nextFromIntact), Is.True);
                Assert.That(nextFromIntact, Is.EqualTo(broken1));

                Assert.That(shipHull.TryGetNextDamageTile(broken1, out var nextFromStage1), Is.True);
                Assert.That(nextFromStage1, Is.EqualTo(broken2));

                Assert.That(shipHull.TryGetNextDamageTile(broken2, out var nextFromStage2), Is.True);
                Assert.That(nextFromStage2, Is.EqualTo(broken3));

                Assert.That(shipHull.TryGetNextDamageTile(broken3, out _), Is.False);

                Assert.That(shipHull.TryGetPreviousDamageTile(broken1, out var previousFromStage1), Is.True);
                Assert.That(previousFromStage1, Is.EqualTo(intact));

                Assert.That(shipHull.TryGetPreviousDamageTile(broken2, out var previousFromStage2), Is.True);
                Assert.That(previousFromStage2, Is.EqualTo(broken1));

                Assert.That(shipHull.TryGetPreviousDamageTile(broken3, out var previousFromStage3), Is.True);
                Assert.That(previousFromStage3, Is.EqualTo(broken2));

                Assert.That(shipHull.GetFloodContribution(intact), Is.EqualTo(0));
                Assert.That(shipHull.GetFloodContribution(broken1), Is.EqualTo(1));
                Assert.That(shipHull.GetFloodContribution(broken2), Is.EqualTo(2));
                Assert.That(shipHull.GetFloodContribution(broken3), Is.EqualTo(3));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RepairRevertsExactlyOneDamageStageAndConsumesOnePlank()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var tileDefinitions = server.ResolveDependency<ITileDefinitionManager>();

        await server.WaitAssertion(() =>
        {
            var broken2 = GetTileId(tileDefinitions, "woodbroken2");
            var broken3 = GetTileId(tileDefinitions, "woodbroken3");
            var gridUid = SpawnSingleTileGrid(mapManager, mapSystem, testMap.MapId, broken3, out var gridComp);
            var user = entMan.SpawnEntity(null, new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
            var plank = entMan.SpawnEntity("MaterialWoodPlank10", new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));

            var repairEvent = CreateCompletedDoAfter(
                entMan,
                user,
                plank,
                gridUid,
                plank,
                new RepairUseEvent(Vector2i.Zero));

            entMan.EventBus.RaiseLocalEvent(plank, repairEvent);

            Assert.That(mapSystem.TryGetTileRef(gridUid, gridComp, Vector2i.Zero, out var tile), Is.True);
            var stack = entMan.GetComponent<StackComponent>(plank);

            Assert.Multiple(() =>
            {
                Assert.That(tile.Tile.TypeId, Is.EqualTo(broken2));
                Assert.That(stack.Count, Is.EqualTo(9));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DrowningAddsDamageContributionAndPassivelyDrainsBelowHalf()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var tileDefinitions = server.ResolveDependency<ITileDefinitionManager>();

        EntityUid gridUid = default;

        await server.WaitAssertion(() =>
        {
            var broken2 = GetTileId(tileDefinitions, "woodbroken2");
            gridUid = SpawnSingleTileGrid(mapManager, mapSystem, testMap.MapId, broken2, out _);

            var drowning = entMan.EnsureComponent<ShipDrowningComponent>(gridUid);
            drowning.DrownLevel = 0;
            drowning.FloodPerDamageStage = 10;
            drowning.PassiveDrainPerTick = 5;
            drowning.PassiveRisePerTick = 5;
            drowning.MaxFloodPerTile = 100;
        });

        await PoolManager.WaitUntil(server, () => entMan.GetComponent<ShipDrowningComponent>(gridUid).DrownLevel == 15, maxTicks: 120);

        await server.WaitAssertion(() =>
        {
            var drowning = entMan.GetComponent<ShipDrowningComponent>(gridUid);

            Assert.Multiple(() =>
            {
                Assert.That(drowning.DrownLevel, Is.EqualTo(15));
                Assert.That(drowning.DrownMaxLevel, Is.EqualTo(100));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DrowningPassivelyRisesAboveHalfWithoutBrokenTiles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var shipHull = entMan.System<SharedShipHullSystem>();

        EntityUid gridUid = default;

        await server.WaitAssertion(() =>
        {
            gridUid = SpawnSingleTileGrid(mapManager, mapSystem, testMap.MapId, shipHull.IntactHullTileId, out _);

            var drowning = entMan.EnsureComponent<ShipDrowningComponent>(gridUid);
            drowning.DrownLevel = 60;
            drowning.FloodPerDamageStage = 10;
            drowning.PassiveDrainPerTick = 5;
            drowning.PassiveRisePerTick = 5;
            drowning.MaxFloodPerTile = 100;
        });

        await PoolManager.WaitUntil(server, () => entMan.GetComponent<ShipDrowningComponent>(gridUid).DrownLevel == 65, maxTicks: 120);

        await server.WaitAssertion(() =>
        {
            var drowning = entMan.GetComponent<ShipDrowningComponent>(gridUid);

            Assert.Multiple(() =>
            {
                Assert.That(drowning.DrownLevel, Is.EqualTo(65));
                Assert.That(drowning.DrownMaxLevel, Is.EqualTo(100));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DrowningDeletesOnlyGridAndPreservesDirectChildren()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var shipHull = entMan.System<SharedShipHullSystem>();

        EntityUid gridUid = default;
        EntityUid child = default;

        await server.WaitAssertion(() =>
        {
            gridUid = SpawnSingleTileGrid(mapManager, mapSystem, testMap.MapId, shipHull.IntactHullTileId, out _);
            child = entMan.SpawnEntity("MaterialWoodPlank1", new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));

            var drowning = entMan.EnsureComponent<ShipDrowningComponent>(gridUid);
            drowning.DrownLevel = 100;
            drowning.FloodPerDamageStage = 10;
            drowning.PassiveDrainPerTick = 5;
            drowning.PassiveRisePerTick = 5;
            drowning.MaxFloodPerTile = 100;
        });

        await PoolManager.WaitUntil(server, () => !entMan.EntityExists(gridUid), maxTicks: 120);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(child), Is.True);

            var childXform = entMan.GetComponent<TransformComponent>(child);
            Assert.Multiple(() =>
            {
                Assert.That(childXform.ParentUid, Is.EqualTo(testMap.MapUid));
                Assert.That(childXform.MapUid, Is.EqualTo(testMap.MapUid));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletingGridPreservesAdminGhostAndHeldItem()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var shipHull = entMan.System<SharedShipHullSystem>();
        var handsSystem = entMan.System<SharedHandsSystem>();

        EntityUid gridUid = default;
        EntityUid ghostUid = default;
        EntityUid heldItem = default;

        await server.WaitAssertion(() =>
        {
            gridUid = SpawnSingleTileGrid(mapManager, mapSystem, testMap.MapId, shipHull.IntactHullTileId, out _);
            ghostUid = entMan.SpawnEntity("AdminObserver", new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
            heldItem = entMan.SpawnEntity("Crowbar", new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));

            Assert.That(handsSystem.TryPickupAnyHand(ghostUid, heldItem), Is.True);
        });

        await server.WaitPost(() => entMan.DeleteEntity(gridUid));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(ghostUid), Is.True);
                Assert.That(entMan.EntityExists(heldItem), Is.True);
                Assert.That(entMan.HasComponent<GhostComponent>(ghostUid), Is.True);
            });

            var ghostXform = entMan.GetComponent<TransformComponent>(ghostUid);
            var heldItemXform = entMan.GetComponent<TransformComponent>(heldItem);

            Assert.Multiple(() =>
            {
                Assert.That(ghostXform.ParentUid, Is.EqualTo(testMap.MapUid));
                Assert.That(ghostXform.GridUid, Is.Null);
                Assert.That(heldItemXform.ParentUid, Is.EqualTo(ghostUid));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletingGridPreservesMobHumanAndHeldItem()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var shipHull = entMan.System<SharedShipHullSystem>();
        var handsSystem = entMan.System<SharedHandsSystem>();

        EntityUid gridUid = default;
        EntityUid humanUid = default;
        EntityUid heldItem = default;

        await server.WaitAssertion(() =>
        {
            gridUid = SpawnSingleTileGrid(mapManager, mapSystem, testMap.MapId, shipHull.IntactHullTileId, out _);
            humanUid = entMan.SpawnEntity("MobHuman", new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
            heldItem = entMan.SpawnEntity("Crowbar", new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));

            Assert.That(handsSystem.TryPickupAnyHand(humanUid, heldItem), Is.True);
        });

        await server.WaitPost(() => entMan.DeleteEntity(gridUid));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(humanUid), Is.True);
                Assert.That(entMan.EntityExists(heldItem), Is.True);
            });

            var humanXform = entMan.GetComponent<TransformComponent>(humanUid);
            var heldItemXform = entMan.GetComponent<TransformComponent>(heldItem);

            Assert.Multiple(() =>
            {
                Assert.That(humanXform.ParentUid, Is.EqualTo(testMap.MapUid));
                Assert.That(humanXform.GridUid, Is.Null);
                Assert.That(heldItemXform.ParentUid, Is.EqualTo(humanUid));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletingGridPreservesMobHumanAndLooseBackpack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var shipHull = entMan.System<SharedShipHullSystem>();

        EntityUid gridUid = default;
        EntityUid humanUid = default;
        EntityUid backpackUid = default;

        await server.WaitAssertion(() =>
        {
            gridUid = SpawnSingleTileGrid(mapManager, mapSystem, testMap.MapId, shipHull.IntactHullTileId, out _);
            humanUid = entMan.SpawnEntity("MobHuman", new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
            backpackUid = entMan.SpawnEntity("ClothingBackpack", new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
        });

        await server.WaitPost(() => entMan.DeleteEntity(gridUid));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(humanUid), Is.True);
                Assert.That(entMan.EntityExists(backpackUid), Is.True);
                Assert.That(entMan.HasComponent<GhostComponent>(humanUid), Is.False);
            });

            var playerXform = entMan.GetComponent<TransformComponent>(humanUid);
            var backpackXform = entMan.GetComponent<TransformComponent>(backpackUid);

            Assert.Multiple(() =>
            {
                Assert.That(playerXform.ParentUid, Is.EqualTo(testMap.MapUid));
                Assert.That(playerXform.GridUid, Is.Null);
                Assert.That(backpackXform.ParentUid, Is.EqualTo(testMap.MapUid));
                Assert.That(backpackXform.GridUid, Is.Null);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnchorToggleDisablesShuttleZerosVelocityAndRestoresItBack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var shipHull = entMan.System<SharedShipHullSystem>();
        var physics = entMan.System<SharedPhysicsSystem>();

        EntityUid gridUid = default;
        EntityUid user = default;
        EntityUid anchorUp = default;

        await server.WaitAssertion(() =>
        {
            gridUid = SpawnSingleTileGrid(mapManager, mapSystem, testMap.MapId, shipHull.IntactHullTileId, out _);
            entMan.EnsureComponent<ShuttleComponent>(gridUid).Enabled = true;

            var body = entMan.GetComponent<PhysicsComponent>(gridUid);
            physics.SetLinearVelocity(gridUid, new Vector2(5f, -2f), body: body);
            physics.SetAngularVelocity(gridUid, 3f, body: body);

            user = entMan.SpawnEntity(null, new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
            var skills = entMan.EnsureComponent<SkillsComponent>(user);
            skills.Levels[SharedSkillsSystem.StrengthId] = 10;

            anchorUp = entMan.SpawnEntity("MedievalAnchorUp", new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));

            var lowerEvent = CreateCompletedDoAfter(
                entMan,
                user,
                anchorUp,
                anchorUp,
                anchorUp,
                new UseAnchorEvent());

            entMan.EventBus.RaiseLocalEvent(anchorUp, lowerEvent);
        });

        await server.WaitRunTicks(1);

        EntityUid anchorDown = default;

        await server.WaitAssertion(() =>
        {
            anchorDown = FindAnchorOnGrid(entMan, gridUid, true);

            var shuttle = entMan.GetComponent<ShuttleComponent>(gridUid);
            var body = entMan.GetComponent<PhysicsComponent>(gridUid);

            Assert.Multiple(() =>
            {
                Assert.That(anchorDown, Is.EqualTo(anchorUp));
                Assert.That(entMan.EntityExists(anchorUp), Is.True);
                Assert.That(entMan.EntityExists(anchorDown), Is.True);
                Assert.That(shuttle.Enabled, Is.False);
                Assert.That(physics.GetMapLinearVelocity(gridUid), Is.EqualTo(Vector2.Zero));
                Assert.That(body.AngularVelocity, Is.EqualTo(0f));
            });
        });

        await server.WaitAssertion(() =>
        {
            var raiseEvent = CreateCompletedDoAfter(
                entMan,
                user,
                anchorDown,
                anchorDown,
                anchorDown,
                new UseAnchorEvent());

            entMan.EventBus.RaiseLocalEvent(anchorDown, raiseEvent);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var newAnchorUp = FindAnchorOnGrid(entMan, gridUid, false);
            var shuttle = entMan.GetComponent<ShuttleComponent>(gridUid);

            Assert.Multiple(() =>
            {
                Assert.That(newAnchorUp, Is.EqualTo(anchorDown));
                Assert.That(entMan.EntityExists(anchorDown), Is.True);
                Assert.That(entMan.EntityExists(newAnchorUp), Is.True);
                Assert.That(shuttle.Enabled, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnedWavesStayOnTheSeaMapInsteadOfBecomingShipChildren()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var shipHull = entMan.System<SharedShipHullSystem>();
        var waveSystem = entMan.System<WaveSystem>();

        EntityUid gridUid = default;
        EntityUid waveUid = default;

        await server.WaitAssertion(() =>
        {
            gridUid = SpawnSingleTileGrid(mapManager, mapSystem, testMap.MapId, shipHull.IntactHullTileId, out _);
            var waveCoords = transform.ToMapCoordinates(new EntityCoordinates(gridUid, new Vector2(5f, 0f)));
            waveUid = waveSystem.SpawnWave(waveCoords, new Vector2(-1f, 0f))!.Value;
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(waveUid), Is.True);
            var waveXform = entMan.GetComponent<TransformComponent>(waveUid);

            Assert.Multiple(() =>
            {
                Assert.That(waveXform.ParentUid, Is.EqualTo(testMap.MapUid));
                Assert.That(waveXform.GridUid, Is.Null);
                Assert.That(waveXform.ParentUid, Is.Not.EqualTo(gridUid));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnedWavesStartMovingWithRequestedVelocity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var physics = entMan.System<SharedPhysicsSystem>();
        var waveSystem = entMan.System<WaveSystem>();

        EntityUid waveUid = default;
        var requestedVelocity = new Vector2(-3f, 1.5f);

        await server.WaitAssertion(() =>
        {
            waveUid = waveSystem.SpawnWave(new MapCoordinates(new Vector2(5f, 5f), testMap.MapId), requestedVelocity)!.Value;
        });

        await server.WaitAssertion(() =>
        {
            var velocity = physics.GetMapLinearVelocity(waveUid);

            Assert.Multiple(() =>
            {
                Assert.That(velocity.X, Is.EqualTo(requestedVelocity.X).Within(0.001f));
                Assert.That(velocity.Y, Is.EqualTo(requestedVelocity.Y).Within(0.001f));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WaveDeletesImmediatelyWhenInsideShipGrid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var shipHull = entMan.System<SharedShipHullSystem>();
        var waveSystem = entMan.System<WaveSystem>();

        var previousStormLevel = cfg.GetCVar(ShipsCCVars.StormLevel);
        var previousWaveMinToBreakLevel = cfg.GetCVar(ShipsCCVars.WaveMinToBreakLevel);

        EntityUid waveUid = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                cfg.SetCVar(ShipsCCVars.StormLevel, 1f);
                cfg.SetCVar(ShipsCCVars.WaveMinToBreakLevel, int.MaxValue);

                var gridUid = SpawnSingleTileGrid(mapManager, mapSystem, testMap.MapId, shipHull.IntactHullTileId, out _);
                entMan.EnsureComponent<ShipDrowningComponent>(gridUid);

                var waveCoords = transform.ToMapCoordinates(new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
                waveUid = waveSystem.SpawnWave(waveCoords, Vector2.Zero)!.Value;
            });

            await pair.RunTicksSync(2);

            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.EntityExists(waveUid), Is.False);
            });
        }
        finally
        {
            await server.WaitAssertion(() =>
            {
                cfg.SetCVar(ShipsCCVars.StormLevel, previousStormLevel);
                cfg.SetCVar(ShipsCCVars.WaveMinToBreakLevel, previousWaveMinToBreakLevel);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public void HelmLeftTurnsCounterClockwiseAndRightTurnsClockwise()
    {
        var method = typeof(HelmSystem).GetMethod("GetSteeringInput", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        var helm = new HelmComponent
        {
            SteeringAngleForMaxTurn = 45f,
            HelmRotation = -45f,
        };

        var leftInput = (float) method!.Invoke(null, [helm])!;
        helm.HelmRotation = 45f;
        var rightInput = (float) method.Invoke(null, [helm])!;

        Assert.Multiple(() =>
        {
            Assert.That(leftInput, Is.EqualTo(1f).Within(0.001f));
            Assert.That(rightInput, Is.EqualTo(-1f).Within(0.001f));
        });
    }

    [Test]
    public void SailPushUsesItsFacingDirection()
    {
        var method = typeof(SailSystem).GetMethod("GetImpulseDirection", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        var south = (Vector2) method!.Invoke(null, [Direction.South.ToAngle()])!;
        var north = (Vector2) method.Invoke(null, [Direction.North.ToAngle()])!;

        Assert.Multiple(() =>
        {
            Assert.That(MathF.Abs(south.X), Is.LessThan(0.001f));
            Assert.That(south.Y, Is.LessThan(0f));

            Assert.That(MathF.Abs(north.X), Is.LessThan(0.001f));
            Assert.That(north.Y, Is.GreaterThan(0f));
        });
    }

    [Test]
    public async Task LooseEntitiesOnSeaMapsGainDrowningAndEventuallyDisappear()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid itemUid = default;

        await server.WaitAssertion(() =>
        {
            entMan.EnsureComponent<SeaComponent>(testMap.MapUid);
            itemUid = entMan.SpawnEntity("MaterialWoodPlank1", new EntityCoordinates(testMap.MapUid, new Vector2(1f, 1f)));
        });

        await PoolManager.WaitUntil(server, () => entMan.TryGetComponent<DrownerComponent>(testMap.MapUid, out _), maxTicks: 120);
        await PoolManager.WaitUntil(server, () => entMan.TryGetComponent<PlayerDrowningComponent>(itemUid, out _), maxTicks: 120);
        await PoolManager.WaitUntil(server, () => !entMan.EntityExists(itemUid), maxTicks: 700);

        await pair.CleanReturnAsync();
    }
}
