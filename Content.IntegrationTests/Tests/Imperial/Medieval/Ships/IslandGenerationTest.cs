using System.Collections.Generic;
using System.Linq;
using Content.Server.Imperial.Medieval.Ships.Islands;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.Ships;

[TestFixture]
public sealed class IslandGenerationTest
{
    [Test]
    public async Task AllIslandsSpawnedTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var res = server.ResolveDependency<IResourceManager>();
        var mapSys = entMan.System<SharedMapSystem>();

        var lowIslands = new List<ResPath>
        {
            new("/Maps/Imperial/Medieval/Islands/IslandLow52.yml"),
        };
        var mediumIslands = new List<ResPath>
        {
            new("/Maps/Imperial/Medieval/Islands/IslandMedium56.yml"),
        };
        var highIslands = new List<ResPath>
        {
            new("/Maps/Imperial/Medieval/Islands/IslandHard10.yml"),
            new("/Maps/Imperial/Medieval/Islands/IslandHard24.yml"),
        };
        const int targetPerRing = 20;
        while (lowIslands.Count < targetPerRing)
            lowIslands.Add(lowIslands[lowIslands.Count % 1]);
        while (mediumIslands.Count < targetPerRing)
            mediumIslands.Add(mediumIslands[mediumIslands.Count % 1]);
        while (highIslands.Count < targetPerRing)
            highIslands.Add(highIslands[highIslands.Count % 2]);

        var expectedCount = lowIslands.Count + mediumIslands.Count + highIslands.Count;

        var allPaths = lowIslands.Concat(mediumIslands).Concat(highIslands);
        await server.WaitAssertion(() =>
        {
            foreach (var path in allPaths)
            {
                var radius = IslandRadiusParser.TryComputeRadius(path, res);
                TestContext.Out.WriteLine($"{path.Filename}: radius = {radius?.ToString() ?? "null"}");
            }
        });

        MapId mapId = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out mapId, runMapInit: false);

            var mapUid = mapSys.GetMap(mapId);
            var comp = entMan.AddComponent<IslandRadialGenerationComponent>(mapUid);
            comp.LowIslands = lowIslands;
            comp.MediumIslands = mediumIslands;
            comp.HighIslands = highIslands;

            server.CfgMan.SetCVar(RTCVars.FailureLogLevel, LogLevel.Fatal);
            mapSys.InitializeMap(mapId);
            server.CfgMan.SetCVar(RTCVars.FailureLogLevel, LogLevel.Error);
        });

        await server.WaitAssertion(() =>
        {
            var grids = mapManager.GetAllGrids(mapId).ToList();
            Assert.That(grids, Has.Count.EqualTo(expectedCount),
                $"Expected {expectedCount} island grids, but found {grids.Count}.");
        });

        await server.WaitPost(() => mapSys.DeleteMap(mapId));
        await pair.CleanReturnAsync();
    }
}
