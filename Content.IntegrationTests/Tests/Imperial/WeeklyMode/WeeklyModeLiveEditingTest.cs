#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Lathe;
using Content.Server.Materials;
using Content.Server.Power.Components;
using Content.Server.Research.Systems;
using Content.Server.Station.Systems;
using Content.Server.WeeklyMode.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.CCVar;
using Content.Shared.Lathe;
using Content.Shared.Maps;
using Content.Shared.Preferences;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial.WeeklyMode;

[TestFixture]
public sealed class WeeklyModeLiveEditingTest : GameTest
{
    private const string EmptyMapPath = "/Maps/Test/empty.yml";
    private const string ForcedRolesMapId = "WeeklyForcedRoleStationMap";
    private const string FirstRecipe = "PowerDrill";
    private const string SecondRecipe = "Welder";
    private const string CargoItem = "SheetSteel";
    private static readonly ProtoId<JobPrototype> Captain = "Captain";
    private static readonly ProtoId<JobPrototype> Passenger = "Passenger";

    [Test]
    public async Task AddUpdateAndRemoveResearchDuringActiveCampaignRefreshesRuntimeDatabase()
    {
        var setId = UniqueSetId();

        await UseIsolatedWeeklyRoot();

        await Server.WaitPost(() =>
        {
            var commandHost = Server.ResolveDependency<IConsoleHost>();
            var weekly = SEntMan.System<WeeklyModeSystem>();
            var research = SEntMan.System<ResearchSystem>();
            StartWeeklySet(weekly, setId);

            var databaseUid = SEntMan.SpawnEntity("WeeklyLiveResearchDatabase", MapCoordinates.Nullspace);
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);

            commandHost.ExecuteCommand($"wm.tech {setId} industrial add WeeklyTools 100 1 {FirstRecipe}");

            Assert.Multiple(() =>
            {
                Assert.That(database.WeeklyModeOnly, Is.True);
                Assert.That(database.WeeklyTechnologies, Has.Count.EqualTo(1));
                Assert.That(database.WeeklyTechnologies[0].TechnologyId, Is.EqualTo("WeeklyTools"));
                Assert.That(database.WeeklyTechnologies[0].Branch, Is.EqualTo("Industrial"));
                Assert.That(database.WeeklyTechnologies[0].Cost, Is.EqualTo(100));
                Assert.That(database.WeeklyTechnologies[0].Tier, Is.EqualTo(1));
                Assert.That(database.WeeklyTechnologies[0].RecipeIds, Is.EqualTo(new[] { FirstRecipe }));
                Assert.That(research.GetAvailableWeeklyTechnologies(databaseUid, database).Select(x => x.TechnologyId),
                    Contains.Item("WeeklyTools"));
            });

            commandHost.ExecuteCommand($"wm.tech.update {setId} WeeklyTools service 75 2 {FirstRecipe} {SecondRecipe}");

            Assert.Multiple(() =>
            {
                Assert.That(database.WeeklyTechnologies, Has.Count.EqualTo(1));
                Assert.That(database.WeeklyTechnologies[0].Branch, Is.EqualTo("CivilianServices"));
                Assert.That(database.WeeklyTechnologies[0].Cost, Is.EqualTo(75));
                Assert.That(database.WeeklyTechnologies[0].Tier, Is.EqualTo(2));
                Assert.That(database.WeeklyTechnologies[0].RecipeIds, Is.EqualTo(new[] { FirstRecipe, SecondRecipe }));
                Assert.That(weekly.ExportConfig(setId), Does.Contain("WeeklyTools"));
                Assert.That(weekly.ExportConfig(setId), Does.Contain(SecondRecipe));
            });

            commandHost.ExecuteCommand($"wm.tech {setId} service remove WeeklyTools");

            Assert.Multiple(() =>
            {
                Assert.That(database.WeeklyModeOnly, Is.True);
                Assert.That(database.WeeklyTechnologies, Is.Empty);
                Assert.That(weekly.ListTechnologies(setId), Does.Contain("empty research tree"));
                Assert.That(weekly.ExportConfig(setId), Does.Not.Contain("WeeklyTools"));
            });
        });
    }

    [Test]
    public async Task PurchasedTechnologyRemovalIsRejectedWithoutForceAndCanBeForced()
    {
        var setId = UniqueSetId();

        await UseIsolatedWeeklyRoot();

        await Server.WaitPost(() =>
        {
            var commandHost = Server.ResolveDependency<IConsoleHost>();
            var weekly = SEntMan.System<WeeklyModeSystem>();
            var research = SEntMan.System<ResearchSystem>();
            StartWeeklySet(weekly, setId);

            var databaseUid = SEntMan.SpawnEntity("WeeklyLiveResearchDatabase", MapCoordinates.Nullspace);
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);

            commandHost.ExecuteCommand($"wm.tech {setId} industrial add PurchasedWeeklyTools 100 1 {FirstRecipe}");

            var technology = database.WeeklyTechnologies.Single();
            research.AddWeeklyTechnology(databaseUid, technology, database);

            Assert.That(weekly.TryRemoveTechnology(setId, "industrial", "PurchasedWeeklyTools", false, out var message),
                Is.False);

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("--force"));
                Assert.That(database.WeeklyUnlockedTechnologies, Contains.Item("PurchasedWeeklyTools"));
                Assert.That(database.UnlockedRecipes.Select(recipe => recipe.Id), Contains.Item(FirstRecipe));
                Assert.That(database.WeeklyTechnologies.Select(entry => entry.TechnologyId), Contains.Item("PurchasedWeeklyTools"));
            });

            commandHost.ExecuteCommand($"wm.tech {setId} industrial remove PurchasedWeeklyTools --force");

            Assert.Multiple(() =>
            {
                Assert.That(database.WeeklyUnlockedTechnologies, Does.Not.Contain("PurchasedWeeklyTools"));
                Assert.That(database.UnlockedRecipes.Select(recipe => recipe.Id), Does.Not.Contain(FirstRecipe));
                Assert.That(database.WeeklyTechnologies.Select(entry => entry.TechnologyId), Does.Not.Contain("PurchasedWeeklyTools"));
                Assert.That(weekly.ExportConfig(setId), Does.Not.Contain("PurchasedWeeklyTools"));
            });
        });
    }

    [Test]
    public async Task AddUpdateAndRemoveCargoDuringActiveCampaignRefreshesRuntimeCatalog()
    {
        var setId = UniqueSetId();

        await UseIsolatedWeeklyRoot();

        await Server.WaitPost(() =>
        {
            var commandHost = Server.ResolveDependency<IConsoleHost>();
            var weekly = SEntMan.System<WeeklyModeSystem>();
            var cargo = SEntMan.System<CargoSystem>();
            StartWeeklySet(weekly, setId);

            var console = CreateCargoConsole();

            commandHost.ExecuteCommand($"wm.cargo {setId} add WeeklySteel Materials 200 false 5 {CargoItem}");

            var products = cargo.GetAvailableWeeklyProducts((console.ConsoleUid, console.Console));
            AssertCargoUiState(console.ConsoleUid, "WeeklySteel", "Materials", 200, false, 5);
            Assert.Multiple(() =>
            {
                Assert.That(products, Has.Count.EqualTo(1));
                Assert.That(products[0].ProductId, Is.EqualTo("WeeklySteel"));
                Assert.That(products[0].Category, Is.EqualTo("Materials"));
                Assert.That(products[0].Cost, Is.EqualTo(200));
                Assert.That(products[0].Amount, Is.EqualTo(5));
                Assert.That(products[0].Boxed, Is.False);
            });

            commandHost.ExecuteCommand($"wm.cargo.update {setId} WeeklySteel Emergency 350 true 2 {CargoItem}");

            products = cargo.GetAvailableWeeklyProducts((console.ConsoleUid, console.Console));
            AssertCargoUiState(console.ConsoleUid, "WeeklySteel", "Emergency", 350, true, 2);
            Assert.Multiple(() =>
            {
                Assert.That(products, Has.Count.EqualTo(1));
                Assert.That(products[0].Category, Is.EqualTo("Emergency"));
                Assert.That(products[0].Cost, Is.EqualTo(350));
                Assert.That(products[0].Amount, Is.EqualTo(2));
                Assert.That(products[0].Boxed, Is.True);
                Assert.That(weekly.ExportConfig(setId), Does.Contain("\"WeeklySteel\""));
            });

            Assert.That(weekly.TryStop(setId, out var message), Is.True, message);
            Assert.That(weekly.TryStart(setId, null, "integration-test-restart", out message), Is.True, message);
            Assert.That(weekly.TryGetActiveWeeklyCargoProducts(out var restartedProducts), Is.True);
            var restartedProduct = restartedProducts.Single(product => product.ProductId == "WeeklySteel");
            Assert.Multiple(() =>
            {
                Assert.That(restartedProduct.Category, Is.EqualTo("Emergency"));
                Assert.That(restartedProduct.Cost, Is.EqualTo(350));
                Assert.That(restartedProduct.Amount, Is.EqualTo(2));
                Assert.That(restartedProduct.Boxed, Is.True);
            });

            commandHost.ExecuteCommand($"wm.cargo {setId} remove WeeklySteel");

            Assert.Multiple(() =>
            {
                Assert.That(cargo.GetAvailableWeeklyProducts((console.ConsoleUid, console.Console)), Is.Empty);
                AssertCargoUiStateDoesNotContain(console.ConsoleUid, "WeeklySteel");
                Assert.That(weekly.ListCargoProducts(setId), Does.Contain("empty cargo catalog"));
            });
        });
    }

    [Test]
    public async Task ExistingWeeklyCargoOrderSurvivesProductRemoval()
    {
        var setId = UniqueSetId();

        await UseIsolatedWeeklyRoot();

        await Server.WaitPost(() =>
        {
            var commandHost = Server.ResolveDependency<IConsoleHost>();
            var weekly = SEntMan.System<WeeklyModeSystem>();
            var cargo = SEntMan.System<CargoSystem>();
            StartWeeklySet(weekly, setId);

            var console = CreateCargoConsole();
            var orderDatabase = console.OrderDatabase;

            commandHost.ExecuteCommand($"wm.cargo {setId} add QueuedWeeklySteel Materials 200 false 5 {CargoItem}");
            Assert.That(weekly.TryGetActiveWeeklyCargoProduct("QueuedWeeklySteel", out var product), Is.True);

            var account = new ProtoId<CargoAccountPrototype>("Cargo");
            var order = new CargoOrderData(1, product, 1, "Test", "Queue survives product removal", account);
            order.Approved = true;
            orderDatabase.Orders[account].Add(order);

            commandHost.ExecuteCommand($"wm.cargo {setId} remove QueuedWeeklySteel");

            Assert.Multiple(() =>
            {
                Assert.That(cargo.GetAvailableWeeklyProducts((console.ConsoleUid, console.Console)), Is.Empty);
                Assert.That(orderDatabase.Orders[account], Has.Count.EqualTo(1));
                Assert.That(orderDatabase.Orders[account][0].IsWeeklyProduct, Is.True);
                Assert.That(orderDatabase.Orders[account][0].WeeklyProduct?.ProductId, Is.EqualTo("QueuedWeeklySteel"));
                Assert.That(orderDatabase.Orders[account][0].WeeklyProduct?.ItemPrototype, Is.EqualTo(CargoItem));
            });
        });
    }

    [Test]
    public async Task WeeklyRecipeUnlockAppearsOnTargetLatheAndProducesResult()
    {
        var setId = UniqueSetId();
        EntityUid securityLathe = default;
        int gasMasksBefore = 0;
        int steelAfterQueue = 0;

        await UseIsolatedWeeklyRoot();

        await Server.WaitPost(() =>
        {
            var commandHost = Server.ResolveDependency<IConsoleHost>();
            var weekly = SEntMan.System<WeeklyModeSystem>();
            var research = SEntMan.System<ResearchSystem>();
            var lathe = SEntMan.System<LatheSystem>();
            var materials = SEntMan.System<MaterialStorageSystem>();
            var ui = SEntMan.System<SharedUserInterfaceSystem>();

            Assert.That(weekly.TryCreateSet(setId, EmptyMapPath, "Weekly recipe test", out var message), Is.True, message);
            commandHost.ExecuteCommand($"wm.recipe {setId} add WeeklyGasMaskRecipe ClothingMaskGas 2 0.01 security Steel:100");
            commandHost.ExecuteCommand($"wm.tech {setId} arsenal add WeeklyGasMasks 0 1 WeeklyGasMaskRecipe");
            Assert.That(weekly.ValidateRecipes(setId), Does.Contain("are valid"));
            Assert.That(weekly.TryStart(setId, null, "integration-test", out message), Is.True, message);

            securityLathe = SEntMan.SpawnEntity("SecurityTechFab", MapCoordinates.Nullspace);
            var medicalLathe = SEntMan.SpawnEntity("MedicalTechFab", MapCoordinates.Nullspace);
            PowerLathe(securityLathe);
            PowerLathe(medicalLathe);

            var securityLatheComp = SEntMan.GetComponent<LatheComponent>(securityLathe);
            var medicalLatheComp = SEntMan.GetComponent<LatheComponent>(medicalLathe);
            var securityDatabase = SEntMan.GetComponent<TechnologyDatabaseComponent>(securityLathe);
            var medicalDatabase = SEntMan.GetComponent<TechnologyDatabaseComponent>(medicalLathe);
            var technology = securityDatabase.WeeklyTechnologies.Single(entry => entry.TechnologyId == "WeeklyGasMasks");
            research.AddWeeklyTechnology(securityLathe, technology, securityDatabase);
            research.AddWeeklyTechnology(medicalLathe, technology, medicalDatabase);

            var securityRecipes = weekly.GetAvailableWeeklyLatheRecipes(securityLathe, securityLatheComp);
            var medicalRecipes = weekly.GetAvailableWeeklyLatheRecipes(medicalLathe, medicalLatheComp);
            Assert.Multiple(() =>
            {
                Assert.That(securityRecipes.Select(recipe => recipe.RecipeId), Is.EqualTo(new[] { "WeeklyGasMaskRecipe" }));
                Assert.That(medicalRecipes.Select(recipe => recipe.RecipeId), Does.Not.Contain("WeeklyGasMaskRecipe"));
                Assert.That(ui.TryGetUiState<LatheUpdateState>(securityLathe, LatheUiKey.Key, out var state), Is.True);
                Assert.That(state!.WeeklyRecipes.Select(recipe => recipe.RecipeId), Contains.Item("WeeklyGasMaskRecipe"));
            });

            Assert.That(materials.TryChangeMaterialAmount(securityLathe, "Steel", 1000), Is.True);
            Assert.That(lathe.TryAddWeeklyToQueue(securityLathe, securityRecipes.Single(), 1, securityLatheComp), Is.True);
            steelAfterQueue = materials.GetMaterialAmount(securityLathe, "Steel");
            Assert.That(steelAfterQueue, Is.EqualTo(900));
            gasMasksBefore = CountEntitiesByPrototype("ClothingMaskGas");
            Assert.That(lathe.TryStartProducing(securityLathe, securityLatheComp), Is.True);
        });

        await RunSeconds(0.2f);

        await Server.WaitPost(() =>
        {
            var materials = SEntMan.System<MaterialStorageSystem>();
            var securityLatheComp = SEntMan.GetComponent<LatheComponent>(securityLathe);

            Assert.Multiple(() =>
            {
                Assert.That(CountEntitiesByPrototype("ClothingMaskGas"), Is.EqualTo(gasMasksBefore + 2));
                Assert.That(materials.GetMaterialAmount(securityLathe, "Steel"), Is.EqualTo(steelAfterQueue));
                Assert.That(securityLatheComp.CurrentRecipe, Is.Null);
                Assert.That(securityLatheComp.Queue, Is.Empty);
            });
        });
    }

    [Test]
    public async Task ForcedRoundStartRoleIgnoresMissingPrefsAndPriorityNever()
    {
        var setId = UniqueSetId();

        await UseIsolatedWeeklyRoot();

        await Server.WaitPost(() =>
        {
            var weekly = SEntMan.System<WeeklyModeSystem>();
            var stationJobs = SEntMan.System<StationJobsSystem>();
            StartWeeklySet(weekly, setId);

            var forcedPlayer = new NetUserId(Guid.NewGuid());
            var competingPlayer = new NetUserId(Guid.NewGuid());
            Assert.That(weekly.TryForceRole(setId, forcedPlayer, "forced-never", Captain, false, "integration-test", out var message),
                Is.True,
                message);

            var station = CreateForcedRoleStation();
            var forcedProfile = HumanoidCharacterProfile.Random()
                .WithJobPriority(Captain, JobPriority.Never)
                .WithJobPriority(Passenger, JobPriority.Medium);
            var competingProfile = HumanoidCharacterProfile.Random()
                .WithJobPriority(Captain, JobPriority.High)
                .WithJobPriority(Passenger, JobPriority.Medium);
            var profiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
            {
                [forcedPlayer] = forcedProfile,
                [competingPlayer] = competingProfile,
            };

            var assigned = stationJobs.AssignJobs(profiles, new[] { station });

            Assert.Multiple(() =>
            {
                Assert.That(forcedProfile.JobPriorities.Keys, Does.Not.Contain(Captain));
                Assert.That(assigned[forcedPlayer].Item1, Is.EqualTo(Captain));
                Assert.That(assigned[forcedPlayer].Item2, Is.EqualTo(station));
                Assert.That(assigned[competingPlayer].Item1, Is.EqualTo(Passenger));
                Assert.That(assigned.Values.Count(value => value.Item1 == Captain), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task ForcedRoundStartRoleReservesSlotForAbsentPlayer()
    {
        var setId = UniqueSetId();

        await UseIsolatedWeeklyRoot();

        await Server.WaitPost(() =>
        {
            var weekly = SEntMan.System<WeeklyModeSystem>();
            var stationJobs = SEntMan.System<StationJobsSystem>();
            StartWeeklySet(weekly, setId);

            var absentPlayer = new NetUserId(Guid.NewGuid());
            var competingPlayer = new NetUserId(Guid.NewGuid());
            Assert.That(weekly.TryForceRole(setId, absentPlayer, "absent-captain", Captain, false, "integration-test", out var message),
                Is.True,
                message);

            var station = CreateForcedRoleStation();
            var profiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
            {
                [competingPlayer] = HumanoidCharacterProfile.Random()
                    .WithJobPriority(Captain, JobPriority.High)
                    .WithJobPriority(Passenger, JobPriority.Medium),
            };

            var assigned = stationJobs.AssignJobs(profiles, new[] { station });

            Assert.Multiple(() =>
            {
                Assert.That(assigned.ContainsKey(absentPlayer), Is.False);
                Assert.That(assigned[competingPlayer].Item1, Is.EqualTo(Passenger));
                Assert.That(assigned.Values.Select(value => value.Item1), Does.Not.Contain(Captain));
            });
        });
    }

    [Test]
    public async Task ForcedRoleConfigRejectsDisabledZeroLimitAndOverLimitJobs()
    {
        await UseIsolatedWeeklyRoot();

        await Server.WaitPost(() =>
        {
            var weekly = SEntMan.System<WeeklyModeSystem>();
            var disabledSetId = UniqueSetId();
            StartWeeklySet(weekly, disabledSetId);

            Assert.That(weekly.TryDisableRoles(disabledSetId, new[] { Captain.Id }, out var message), Is.True, message);
            Assert.That(weekly.TryForceRole(disabledSetId, new NetUserId(Guid.NewGuid()), "disabled-captain", Captain, false, "integration-test", out message),
                Is.False);
            Assert.That(message, Does.Contain("disabled"));
            Assert.That(weekly.TryStop(disabledSetId, out message), Is.True, message);

            var zeroLimitSetId = UniqueSetId();
            StartWeeklySet(weekly, zeroLimitSetId);
            Assert.That(weekly.TrySetRoleLimit(zeroLimitSetId, Captain, 0, out message), Is.True, message);
            Assert.That(weekly.TryForceRole(zeroLimitSetId, new NetUserId(Guid.NewGuid()), "zero-limit-captain", Captain, false, "integration-test", out message),
                Is.False);
            Assert.That(message, Does.Contain("limit is 0"));
            Assert.That(weekly.TryStop(zeroLimitSetId, out message), Is.True, message);

            var overLimitSetId = UniqueSetId();
            StartWeeklySet(weekly, overLimitSetId);
            Assert.That(weekly.TrySetRoleLimit(overLimitSetId, Captain, 1, out message), Is.True, message);
            Assert.That(weekly.TryForceRole(overLimitSetId, new NetUserId(Guid.NewGuid()), "first-captain", Captain, false, "integration-test", out message),
                Is.True,
                message);
            Assert.That(weekly.TryForceRole(overLimitSetId, new NetUserId(Guid.NewGuid()), "second-captain", Captain, false, "integration-test", out message),
                Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("exceed campaign role limit"));
                Assert.That(weekly.ListForcedRoles(overLimitSetId), Does.Contain("first-captain"));
                Assert.That(weekly.ListForcedRoles(overLimitSetId), Does.Not.Contain("second-captain"));
            });
        });
    }

    [Test]
    public async Task ForcedRoleAssignmentPersistsAfterCampaignRestart()
    {
        var setId = UniqueSetId();

        await UseIsolatedWeeklyRoot();

        await Server.WaitPost(() =>
        {
            var weekly = SEntMan.System<WeeklyModeSystem>();
            StartWeeklySet(weekly, setId);

            var userId = new NetUserId(Guid.NewGuid());
            Assert.That(weekly.TryForceRole(setId, userId, "persistent-captain", Captain, true, "integration-test", out var message),
                Is.True,
                message);
            Assert.That(weekly.TryStop(setId, out message), Is.True, message);
            Assert.That(weekly.TryStart(setId, null, "integration-test-restart", out message), Is.True, message);

            Assert.Multiple(() =>
            {
                Assert.That(weekly.ListForcedRoles(setId), Does.Contain(userId.UserId.ToString()));
                Assert.That(weekly.ListForcedRoles(setId), Does.Contain("persistent-captain"));
                Assert.That(weekly.ListForcedRoles(setId), Does.Contain("Captain"));
                Assert.That(weekly.ListForcedRoles(setId), Does.Contain("bypassPlaytime=True"));
            });
        });
    }

    private async Task UseIsolatedWeeklyRoot()
    {
        await OverrideCVar(Side.Server, CCVars.WeeklyModeDataRoot, $"/weekly-mode-tests/{Guid.NewGuid():N}");
    }

    private static string UniqueSetId()
    {
        return $"live-edit-{Guid.NewGuid():N}";
    }

    private void StartWeeklySet(WeeklyModeSystem weekly, string setId)
    {
        Assert.That(weekly.TryCreateSet(setId, EmptyMapPath, "Live edit test", out var message), Is.True, message);
        Assert.That(weekly.TryStart(setId, null, "integration-test", out message), Is.True, message);
    }

    private EntityUid CreateForcedRoleStation()
    {
        var prototype = Server.ResolveDependency<IPrototypeManager>().Index<GameMapPrototype>(ForcedRolesMapId);
        return SEntMan.System<StationSystem>().InitializeNewStation(prototype.Stations["Station"], null, "Weekly forced role test");
    }

    private CargoConsoleFixture CreateCargoConsole()
    {
        var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        SEntMan.EnsureComponent<StationDataComponent>(station);
        var orderDatabase = SEntMan.EnsureComponent<StationCargoOrderDatabaseComponent>(station);
        orderDatabase.Orders[new ProtoId<CargoAccountPrototype>("Cargo")] = [];

        var console = SEntMan.SpawnEntity("WeeklyLiveCargoConsole", MapCoordinates.Nullspace);
        var consoleComponent = SEntMan.GetComponent<CargoOrderConsoleComponent>(console);

        var tracker = SEntMan.EnsureComponent<StationTrackerComponent>(console);
        var stationSystem = SEntMan.System<StationSystem>();
        stationSystem.SetStation((console, tracker), station);

        return new CargoConsoleFixture(station, console, consoleComponent, orderDatabase);
    }

    private void PowerLathe(EntityUid uid)
    {
        if (!SEntMan.HasComponent<ApcPowerReceiverComponent>(uid))
            return;

        SEntMan.RemoveComponent<ApcPowerReceiverComponent>(uid);
    }

    private int CountEntitiesByPrototype(string prototypeId)
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == prototypeId)
                count++;
        }

        return count;
    }

    private void AssertCargoUiState(EntityUid consoleUid, string productId, string category, int cost, bool boxed, int amount)
    {
        var ui = SEntMan.System<SharedUserInterfaceSystem>();
        Assert.That(ui.TryGetUiState<CargoConsoleInterfaceState>(consoleUid, CargoConsoleUiKey.Orders, out var state),
            Is.True,
            "Cargo order console did not receive a UI state update.");

        var product = state!.WeeklyProducts.Single(product => product.ProductId == productId);
        Assert.Multiple(() =>
        {
            Assert.That(product.Category, Is.EqualTo(category));
            Assert.That(product.Cost, Is.EqualTo(cost));
            Assert.That(product.Boxed, Is.EqualTo(boxed));
            Assert.That(product.Amount, Is.EqualTo(amount));
        });
    }

    private void AssertCargoUiStateDoesNotContain(EntityUid consoleUid, string productId)
    {
        var ui = SEntMan.System<SharedUserInterfaceSystem>();
        Assert.That(ui.TryGetUiState<CargoConsoleInterfaceState>(consoleUid, CargoConsoleUiKey.Orders, out var state),
            Is.True,
            "Cargo order console did not receive a UI state update.");

        Assert.That(state!.WeeklyProducts.Select(product => product.ProductId), Does.Not.Contain(productId));
    }

    private readonly record struct CargoConsoleFixture(
        EntityUid Station,
        EntityUid ConsoleUid,
        CargoOrderConsoleComponent Console,
        StationCargoOrderDatabaseComponent OrderDatabase);

    [TestPrototypes]
    private const string Prototypes = @"
- type: gameMap
  id: WeeklyForcedRoleStationMap
  mapName: WeeklyForcedRoleStationMap
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Station:
      mapNameTemplate: Weekly forced role test
      stationProto: StandardNanotrasenStation
      components:
        - type: StationJobs
          availableJobs:
            Passenger: [-1, -1]
            Captain: [1, 1]

- type: entity
  id: WeeklyLiveResearchDatabase
  components:
  - type: TechnologyDatabase
    supportedDisciplines:
    - Industrial
    - CivilianServices

- type: entity
  id: WeeklyLiveCargoConsole
  components:
  - type: CargoOrderConsole
  - type: StationTracker
  - type: UserInterface
    interfaces:
      enum.CargoConsoleUiKey.Orders:
        type: CargoOrderConsoleBoundUserInterface
";
}
