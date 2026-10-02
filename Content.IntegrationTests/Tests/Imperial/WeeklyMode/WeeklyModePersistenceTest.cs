#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.WeeklyMode.Systems;
using Content.Shared.WeeklyMode;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Imperial.WeeklyMode;

[TestFixture]
public sealed class WeeklyModePersistenceTest : GameTest
{
    private const string AutonomousMobPrototype = "WeeklyPersistenceAutonomousMob";
    private const string ExcludedMobPrototype = "WeeklyPersistenceExcludedMob";
    private const string PlayerBodyPrototype = "WeeklyPersistencePlayerBody";
    private const string BorgPrototype = "WeeklyPersistenceBorg";
    private const string EmptyMapPath = "/Maps/Test/empty.yml";

    [Test]
    public async Task AutonomousMobIsIncludedOnlyWhenPersistenceIsEnabled()
    {
        await Server.WaitPost(() =>
        {
            var weekly = SEntMan.System<WeeklyModeSystem>();
            var mob = SEntMan.SpawnEntity(AutonomousMobPrototype, MapCoordinates.Nullspace);

            var enabledSet = NewSet();
            enabledSet.PersistAutonomousMobs = true;
            var enabled = weekly.CollectSnapshotExclusionsForTest(enabledSet);

            var disabledSet = NewSet();
            disabledSet.PersistAutonomousMobs = false;
            var disabled = weekly.CollectSnapshotExclusionsForTest(disabledSet);

            Assert.Multiple(() =>
            {
                Assert.That(enabled.ExcludedRoots, Does.Not.Contain(mob));
                Assert.That(enabled.ForceMapSavablePrototypes, Contains.Item(AutonomousMobPrototype));
                Assert.That(enabled.AutonomousMobsIncluded, Is.GreaterThanOrEqualTo(1));

                Assert.That(disabled.ExcludedRoots, Contains.Item(mob));
                Assert.That(disabled.ForceMapSavablePrototypes, Does.Not.Contain(AutonomousMobPrototype));
                Assert.That(disabled.AutonomousMobsExcluded, Is.GreaterThanOrEqualTo(1));
            });
        });
    }

    [Test]
    public async Task ExcludedMobPrototypeIsNeverIncluded()
    {
        await Server.WaitPost(() =>
        {
            var weekly = SEntMan.System<WeeklyModeSystem>();
            var mob = SEntMan.SpawnEntity(ExcludedMobPrototype, MapCoordinates.Nullspace);
            var set = NewSet();
            set.PersistAutonomousMobs = true;
            set.ExcludedMobPrototypes.Add(ExcludedMobPrototype);

            var result = weekly.CollectSnapshotExclusionsForTest(set);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExcludedRoots, Contains.Item(mob));
                Assert.That(result.ForceMapSavablePrototypes, Does.Not.Contain(ExcludedMobPrototype));
                Assert.That(result.AutonomousMobsExcluded, Is.GreaterThanOrEqualTo(1));
            });
        });
    }

    [Test]
    public async Task PlayerBodyWithMindIsExcluded()
    {
        await Server.WaitPost(() =>
        {
            var weekly = SEntMan.System<WeeklyModeSystem>();
            var body = SEntMan.SpawnEntity(PlayerBodyPrototype, MapCoordinates.Nullspace);

            var set = NewSet();
            set.PersistAutonomousMobs = true;

            var result = weekly.CollectSnapshotExclusionsForTest(set);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExcludedRoots, Contains.Item(body));
                Assert.That(result.ForceMapSavablePrototypes, Does.Not.Contain(PlayerBodyPrototype));
                Assert.That(result.PlayerBodiesExcluded, Is.GreaterThanOrEqualTo(1));
            });
        });
    }

    [Test]
    public async Task PlayerControlledBorgChassisIsPersistedOnlyWhenEnabled()
    {
        await Server.WaitPost(() =>
        {
            var weekly = SEntMan.System<WeeklyModeSystem>();
            var borg = SEntMan.SpawnEntity(BorgPrototype, MapCoordinates.Nullspace);

            var enabledSet = NewSet();
            enabledSet.PersistPlayerControlledBorgs = true;
            var enabled = weekly.CollectSnapshotExclusionsForTest(enabledSet);

            var disabledSet = NewSet();
            disabledSet.PersistPlayerControlledBorgs = false;
            var disabled = weekly.CollectSnapshotExclusionsForTest(disabledSet);

            Assert.Multiple(() =>
            {
                Assert.That(enabled.ExcludedRoots, Does.Not.Contain(borg));
                Assert.That(enabled.ForceMapSavablePrototypes, Contains.Item(BorgPrototype));
                Assert.That(enabled.BorgChassisIncluded, Is.GreaterThanOrEqualTo(1));

                Assert.That(disabled.ExcludedRoots, Contains.Item(borg));
                Assert.That(disabled.ForceMapSavablePrototypes, Does.Not.Contain(BorgPrototype));
                Assert.That(disabled.BorgChassisExcluded, Is.GreaterThanOrEqualTo(1));
            });
        });
    }

    private static WeeklyModeSet NewSet()
    {
        return new WeeklyModeSet
        {
            SetId = "weekly-persistence-test",
            BaseMapPath = EmptyMapPath,
            PersistAutonomousMobs = true,
            PersistPlayerControlledBorgs = true,
            ExcludedMobPrototypes = new List<string>(),
        };
    }

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: WeeklyPersistenceAutonomousMob
  save: false
  components:
  - type: MobState
  - type: MindContainer

- type: entity
  id: WeeklyPersistenceExcludedMob
  save: false
  components:
  - type: MobState
  - type: MindContainer

- type: entity
  id: WeeklyPersistencePlayerBody
  save: false
  components:
  - type: MobState
  - type: MindContainer
    hasMind: true

- type: entity
  id: WeeklyPersistenceBorg
  save: false
  components:
  - type: MobState
  - type: MindContainer
    hasMind: true
  - type: BorgChassis
    active: true
";
}
