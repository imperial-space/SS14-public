#nullable enable
using Content.Server.WeeklyMode.Storage;
using Content.Server.WeeklyMode.Systems;
using Content.Shared.WeeklyMode;
using Moq;
using NUnit.Framework;
using Robust.Shared.ContentPack;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Content.Tests.Server.Imperial.WeeklyMode;

[TestFixture]
public sealed class WeeklyModeStoreTest
{
    [Test]
    public void SafeIdsAllowOnlyCommandFacingIdentifiers()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WeeklyModeStore.IsSafeId("season-01"), Is.True);
            Assert.That(WeeklyModeStore.IsSafeId("season_one.snapshot"), Is.True);
            Assert.That(WeeklyModeStore.IsSafeId("../season"), Is.False);
            Assert.That(WeeklyModeStore.IsSafeId("..\\season"), Is.False);
            Assert.That(WeeklyModeStore.IsSafeId("season/one"), Is.False);
            Assert.That(WeeklyModeStore.IsSafeId("C:/weekly"), Is.False);
            Assert.That(WeeklyModeStore.IsSafeId("/weekly"), Is.False);
            Assert.That(WeeklyModeStore.IsSafeId("weekly mode"), Is.False);
            Assert.That(WeeklyModeStore.IsSafeId(""), Is.False);
        });
    }

    [Test]
    public void ContainerPatchIsStructuredData()
    {
        var patch = new WeeklyContainerPatch
        {
            Entries =
            {
                new WeeklyContainerEntry
                {
                    OwnerYamlUid = 1,
                    ContainerId = "storagebase",
                    ChildYamlUid = 2,
                    Index = 0,
                    OwnerDepth = 0,
                }
            }
        };

        Assert.Multiple(() =>
        {
            Assert.That(patch.SchemaVersion, Is.EqualTo(WeeklyModeSet.CurrentSchemaVersion));
            Assert.That(patch.Entries, Has.Count.EqualTo(1));
            Assert.That(patch.Entries[0].ContainerId, Is.EqualTo("storagebase"));
        });
    }

    [Test]
    public void RollbackBackupKindSerializesAsString()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() },
        };

        var metadata = new WeeklySnapshotMetadata
        {
            SnapshotId = "rollback-backup-20260101-000000",
            SetId = "season-01",
            Kind = WeeklySnapshotKind.RollbackBackup,
        };

        var json = JsonSerializer.Serialize(metadata, options);
        Assert.That(json, Does.Contain("RollbackBackup"));
    }

    [Test]
    public void SerializedSnapshotMapNameReadsMapRoot()
    {
        var data = SerializedMapData("Dev");

        Assert.Multiple(() =>
        {
            Assert.That(WeeklyModeSystem.TryReadSerializedMapName(data, out var mapName, out var message), Is.True, message);
            Assert.That(mapName, Is.EqualTo("Dev"));
        });
    }

    [Test]
    public void SerializedSnapshotMapNameRejectsMissingMetadataName()
    {
        var data = SerializedMapData(null);

        Assert.Multiple(() =>
        {
            Assert.That(WeeklyModeSystem.TryReadSerializedMapName(data, out _, out var message), Is.False);
            Assert.That(message, Does.Contain("MetaData.name"));
        });
    }

    [Test]
    public void SnapshotMetadataPersistsSavedMapIdentity()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;
        const string setId = "season-01";
        const string snapshotId = "manual-20260614-095959";
        var temp = store.TempSnapshotDirectory(setId, snapshotId);

        WriteFullBundle(store, ctx.UserData, temp, setId, snapshotId, "identity metadata");
        store.ReplaceSnapshotDirectory(temp, setId, snapshotId);

        Assert.Multiple(() =>
        {
            Assert.That(store.TryLoadSnapshot(setId, snapshotId, out var metadata), Is.True);
            Assert.That(metadata!.SavedMapId, Is.EqualTo(7));
            Assert.That(metadata.SavedMapName, Is.EqualTo("Packed"));
            Assert.That(metadata.SavedGridIds, Is.EqualTo(new[] { 8, 9 }));
            Assert.That(metadata.EntityCount, Is.EqualTo(42));
        });
    }

    [Test]
    public void SerializedSnapshotSanitizerRemovesRuntimeStationAndInvalidContainerReferences()
    {
        var stationMember = new MappingDataNode
        {
            { "type", new ValueDataNode("StationMember") },
            { "station", new ValueDataNode("invalid") },
        };
        var suitSensor = new MappingDataNode
        {
            { "type", new ValueDataNode("SuitSensor") },
            { "station", new ValueDataNode("invalid") },
            { "user", new ValueDataNode("invalid") },
            { "mode", new ValueDataNode("SensorVitals") },
        };
        var container = new MappingDataNode
        {
            { "type", new ValueDataNode("ContainerContainer") },
            {
                "containers",
                new MappingDataNode
                {
                    {
                        "entity_storage",
                        new MappingDataNode
                        {
                            {
                                "ents",
                                new SequenceDataNode(
                                    new ValueDataNode("invalid"),
                                    new ValueDataNode("42"))
                            },
                        }
                    },
                    {
                        "slot",
                        new MappingDataNode
                        {
                            { "ent", new ValueDataNode("invalid") },
                        }
                    },
                }
            },
        };
        var containerFill = new MappingDataNode
        {
            { "type", new ValueDataNode("ContainerFill") },
        };
        var poweredLight = new MappingDataNode
        {
            { "type", new ValueDataNode("PoweredLight") },
            { "hasLampOnSpawn", new ValueDataNode("LightTube") },
        };
        var inheritedPoweredLight = new MappingDataNode
        {
            { "type", new ValueDataNode("PoweredLight") },
        };
        var itemSlots = new MappingDataNode
        {
            { "type", new ValueDataNode("ItemSlots") },
            {
                "slots",
                new MappingDataNode
                {
                    {
                        "cell",
                        new MappingDataNode
                        {
                            { "startingItem", new ValueDataNode("PowerCellSmall") },
                        }
                    },
                }
            },
        };
        var actionGrant = new MappingDataNode
        {
            { "type", new ValueDataNode("ActionGrant") },
            {
                "actions",
                new SequenceDataNode(
                    new ValueDataNode("ActionVendingThrowMaggotson"),
                    new ValueDataNode("ActionToggleLight"))
            },
        };
        var vendingMachine = new MappingDataNode
        {
            { "type", new ValueDataNode("VendingMachine") },
            { "initialStockQuality", new ValueDataNode("1") },
        };
        var bin = new MappingDataNode
        {
            { "type", new ValueDataNode("Bin") },
            {
                "initialContents",
                new SequenceDataNode(
                    new ValueDataNode("Paper"),
                    new ValueDataNode("DrinkWaterCup"))
            },
        };
        var cartridgeLoader = new MappingDataNode
        {
            { "type", new ValueDataNode("CartridgeLoader") },
            {
                "preinstalled",
                new SequenceDataNode(
                    new ValueDataNode("CrewManifestCartridge"),
                    new ValueDataNode("NanoTaskCartridge"))
            },
        };
        var lightReplacer = new MappingDataNode
        {
            { "type", new ValueDataNode("LightReplacer") },
            {
                "contents",
                new SequenceDataNode(
                    new MappingDataNode
                    {
                        { "id", new ValueDataNode("LightTube") },
                        { "amount", new ValueDataNode("8") },
                    },
                    new MappingDataNode
                    {
                        { "id", new ValueDataNode("LightBulb") },
                        { "amount", new ValueDataNode("5") },
                    })
            },
        };
        var actor = new MappingDataNode
        {
            { "type", new ValueDataNode("Actor") },
        };
        var mindContainer = new MappingDataNode
        {
            { "type", new ValueDataNode("MindContainer") },
            { "mind", new ValueDataNode("99") },
            { "hasMind", new ValueDataNode("true") },
        };
        var data = SerializedMapData(
            "Saltern",
            stationMember,
            suitSensor,
            container,
            containerFill,
            poweredLight,
            inheritedPoweredLight,
            itemSlots,
            actionGrant,
            vendingMachine,
            bin,
            cartridgeLoader,
            lightReplacer,
            actor,
            mindContainer);
        var entity = SerializedEntity(data);
        entity.Add("mapInit", new ValueDataNode("true"));
        entity.Add("paused", new ValueDataNode("true"));
        var mapComponent = SerializedComponents(data)
            .OfType<MappingDataNode>()
            .Single(x => ComponentType(x) == "Map");
        mapComponent.Add("mapInitialized", new ValueDataNode("True"));
        mapComponent.Add("mapPaused", new ValueDataNode("True"));

        var result = WeeklyModeSystem.SanitizeSerializedSnapshot(data);
        var components = SerializedComponents(data);

        Assert.Multiple(() =>
        {
            Assert.That(result.RemovedStationMembers, Is.EqualTo(1));
            Assert.That(result.RemovedSuitSensorEntityReferences, Is.EqualTo(2));
            Assert.That(result.RemovedInvalidContainerReferences, Is.EqualTo(2));
            Assert.That(result.ResetMapInitializationFields, Is.EqualTo(4));
            Assert.That(result.ResetMindContainers, Is.EqualTo(3));
            Assert.That(result.SuppressedMapInitOnlyComponents, Is.EqualTo(1));
            Assert.That(result.SuppressedStartingItems, Is.EqualTo(23));
            Assert.That(entity.ContainsKey("mapInit"), Is.False);
            Assert.That(entity.ContainsKey("paused"), Is.False);
            Assert.That(mapComponent.ContainsKey("mapInitialized"), Is.False);
            Assert.That(mapComponent.ContainsKey("mapPaused"), Is.False);
            Assert.That(components.OfType<MappingDataNode>().Any(x => ComponentType(x) == "StationMember"), Is.False);
            Assert.That(components.OfType<MappingDataNode>().Any(x => ComponentType(x) == "Actor"), Is.False);
            Assert.That(components.OfType<MappingDataNode>().Any(x => ComponentType(x) == "ContainerFill"), Is.False);
            Assert.That(mindContainer.ContainsKey("mind"), Is.False);
            Assert.That(mindContainer.Get<ValueDataNode>("hasMind").Value, Is.EqualTo("false"));
            Assert.That(suitSensor.ContainsKey("station"), Is.False);
            Assert.That(suitSensor.ContainsKey("user"), Is.False);
            Assert.That(suitSensor.ContainsKey("mode"), Is.True);
            Assert.That(entity.Get<SequenceDataNode>("missingComponents")
                .Select(x => ((ValueDataNode) x).Value), Contains.Item("ContainerFill"));

            var containers = container.Get<MappingDataNode>("containers");
            var storage = containers.Get<MappingDataNode>("entity_storage");
            var ents = storage.Get<SequenceDataNode>("ents");
            Assert.That(ents.Select(x => ((ValueDataNode) x).Value), Is.EqualTo(new[] { "42" }));

            var slot = containers.Get<MappingDataNode>("slot");
            Assert.That(slot.Get<ValueDataNode>("ent").IsNull, Is.True);

            Assert.That(poweredLight.Get<ValueDataNode>("hasLampOnSpawn").IsNull, Is.True);
            Assert.That(inheritedPoweredLight.Get<ValueDataNode>("hasLampOnSpawn").IsNull, Is.True);
            Assert.That(itemSlots.Get<MappingDataNode>("slots")
                .Get<MappingDataNode>("cell")
                .Get<ValueDataNode>("startingItem")
                .IsNull, Is.True);
            Assert.That(actionGrant.Get<SequenceDataNode>("actions"), Is.Empty);
            Assert.That(vendingMachine.Get<ValueDataNode>("initialStockQuality").Value, Is.EqualTo("1"));
            Assert.That(vendingMachine.Get<ValueDataNode>("suppressInitialRestock").Value, Is.EqualTo("true"));
            Assert.That(bin.Get<SequenceDataNode>("initialContents"), Is.Empty);
            Assert.That(cartridgeLoader.Get<SequenceDataNode>("preinstalled"), Is.Empty);
            Assert.That(lightReplacer.Get<SequenceDataNode>("contents"), Is.Empty);
        });
    }

    [Test]
    public void SerializedSnapshotSanitizerClearsStaleBuckleStrapAndJointReferences()
    {
        var buckle = new MappingDataNode
        {
            { "type", new ValueDataNode("Buckle") },
            { "buckledTo", new ValueDataNode("invalid") },
            { "buckleTime", new ValueDataNode("00:00:05") },
        };
        var strap = new MappingDataNode
        {
            { "type", new ValueDataNode("Strap") },
            {
                "buckledEntities",
                new SequenceDataNode(
                    new ValueDataNode("invalid"),
                    new ValueDataNode("42"))
            },
        };
        var joint = new MappingDataNode
        {
            { "type", new ValueDataNode("Joint") },
            { "relay", new ValueDataNode("invalid") },
            {
                "joints",
                new MappingDataNode
                {
                    {
                        "broken",
                        new MappingDataNode
                        {
                            { "bodyA", new ValueDataNode("1") },
                            { "bodyB", new ValueDataNode("invalid") },
                        }
                    },
                    {
                        "valid",
                        new MappingDataNode
                        {
                            { "bodyA", new ValueDataNode("1") },
                            { "bodyB", new ValueDataNode("42") },
                        }
                    },
                }
            },
        };
        var data = SerializedMapData("Saltern", buckle, strap, joint);

        var result = WeeklyModeSystem.SanitizeSerializedSnapshot(data);

        Assert.Multiple(() =>
        {
            Assert.That(result.RemovedStaleBuckleReferences, Is.EqualTo(1));
            Assert.That(result.RemovedStaleStrapReferences, Is.EqualTo(1));
            Assert.That(result.RemovedStaleJointReferences, Is.EqualTo(2));
            Assert.That(buckle.Get<ValueDataNode>("buckledTo").IsNull, Is.True);
            Assert.That(buckle.ContainsKey("buckleTime"), Is.False);
            Assert.That(strap.Get<SequenceDataNode>("buckledEntities")
                .Select(x => ((ValueDataNode) x).Value), Is.EqualTo(new[] { "42" }));
            Assert.That(joint.Get<ValueDataNode>("relay").IsNull, Is.True);
            Assert.That(joint.Get<MappingDataNode>("joints").ContainsKey("broken"), Is.False);
            Assert.That(joint.Get<MappingDataNode>("joints").ContainsKey("valid"), Is.True);
        });
    }

    [Test]
    public void SerializedSnapshotSanitizerPreservesValidBuckleAndStrapReferences()
    {
        var buckle = new MappingDataNode
        {
            { "type", new ValueDataNode("Buckle") },
            { "buckledTo", new ValueDataNode("42") },
            { "buckleTime", new ValueDataNode("00:00:05") },
        };
        var strap = new MappingDataNode
        {
            { "type", new ValueDataNode("Strap") },
            {
                "buckledEntities",
                new SequenceDataNode(new ValueDataNode("42"))
            },
        };
        var data = SerializedMapData("Saltern", buckle, strap);

        var result = WeeklyModeSystem.SanitizeSerializedSnapshot(data);

        Assert.Multiple(() =>
        {
            Assert.That(result.RemovedStaleBuckleReferences, Is.Zero);
            Assert.That(result.RemovedStaleStrapReferences, Is.Zero);
            Assert.That(buckle.Get<ValueDataNode>("buckledTo").Value, Is.EqualTo("42"));
            Assert.That(buckle.ContainsKey("buckleTime"), Is.True);
            Assert.That(strap.Get<SequenceDataNode>("buckledEntities")
                .Select(x => ((ValueDataNode) x).Value), Is.EqualTo(new[] { "42" }));
        });
    }

    [Test]
    public void SerializedSnapshotSanitizerSavesBorgChassisDisabledWithoutMind()
    {
        var mindContainer = new MappingDataNode
        {
            { "type", new ValueDataNode("MindContainer") },
            { "mind", new ValueDataNode("99") },
            { "hasMind", new ValueDataNode("true") },
        };
        var borg = new MappingDataNode
        {
            { "type", new ValueDataNode("BorgChassis") },
            { "active", new ValueDataNode("true") },
        };
        var data = SerializedMapData("Saltern", mindContainer, borg);

        var result = WeeklyModeSystem.SanitizeSerializedSnapshot(data);

        Assert.Multiple(() =>
        {
            Assert.That(result.ResetMindContainers, Is.EqualTo(2));
            Assert.That(result.DisabledBorgChassis, Is.EqualTo(1));
            Assert.That(mindContainer.ContainsKey("mind"), Is.False);
            Assert.That(mindContainer.Get<ValueDataNode>("hasMind").Value, Is.EqualTo("false"));
            Assert.That(borg.Get<ValueDataNode>("active").Value, Is.EqualTo("false"));
        });
    }

    [Test]
    public void TempSnapshotDirectoryBecomesFinalDirectory()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;
        const string setId = "season-01";
        const string snapshotId = "manual-20260614-100000";
        var temp = store.TempSnapshotDirectory(setId, snapshotId);

        ctx.UserData.CreateDir(temp);
        ctx.UserData.WriteAllText(temp / "marker.txt", "new snapshot");

        store.ReplaceSnapshotDirectory(temp, setId, snapshotId);

        Assert.Multiple(() =>
        {
            Assert.That(ctx.UserData.Exists(temp), Is.False);
            Assert.That(ctx.UserData.IsDir(store.SnapshotDirectory(setId, snapshotId)), Is.True);
            Assert.That(ctx.UserData.ReadAllText(store.SnapshotDirectory(setId, snapshotId) / "marker.txt"), Is.EqualTo("new snapshot"));
        });
    }

    [Test]
    public void SaveStyleBundleCreatesCompleteSnapshot()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;
        const string setId = "season-01";
        const string snapshotId = "manual-20260614-100001";
        var temp = store.TempSnapshotDirectory(setId, snapshotId);

        WriteFullBundle(store, ctx.UserData, temp, setId, snapshotId, "complete bundle");

        store.ReplaceSnapshotDirectory(temp, setId, snapshotId);

        Assert.Multiple(() =>
        {
            Assert.That(store.SnapshotExists(setId, snapshotId), Is.True);
            Assert.That(ctx.UserData.Exists(store.SnapshotStationPath(setId, snapshotId)), Is.True);
            Assert.That(ctx.UserData.Exists(store.SnapshotMetadataPath(setId, snapshotId)), Is.True);
            Assert.That(ctx.UserData.Exists(store.SnapshotRoleOverridesPath(setId, snapshotId)), Is.True);
            Assert.That(ctx.UserData.Exists(store.SnapshotContainerPatchPath(setId, snapshotId)), Is.True);
            Assert.That(ctx.UserData.Exists(store.SnapshotIntegrityPath(setId, snapshotId)), Is.True);
        });
    }

    [Test]
    public void CreatedSnapshotIsVisibleToSnapshotListingInputs()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;
        const string setId = "season-01";
        const string snapshotId = "manual-20260614-100002";
        var set = store.CreateSet(setId, "Packed", 10, 8);
        var temp = store.TempSnapshotDirectory(setId, snapshotId);

        WriteFullBundle(store, ctx.UserData, temp, setId, snapshotId, "listed bundle");
        store.ReplaceSnapshotDirectory(temp, setId, snapshotId);
        set.Snapshots.Add(snapshotId);
        store.SaveSet(set);

        var loaded = store.LoadSet(setId);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Snapshots, Contains.Item(snapshotId));
            Assert.That(store.TryLoadSnapshot(setId, snapshotId, out var metadata), Is.True);
            Assert.That(metadata!.Notes, Is.EqualTo("listed bundle"));
        });
    }

    [Test]
    public void CampaignConfigPersistsRoleLimitsAutosaveWarningAndCyrillicAliases()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;
        const string setId = "season-01";
        var set = store.CreateSet(setId, "Packed", 30, 8, baseMapPath: "/Maps/saltern.yml");

        set.AutosaveWarningMinutes = 2;
        set.DefaultRoleAliases["Passenger"] = "Поселенец";
        set.DefaultRoleLimits["Passenger"] = 20;
        set.DefaultRoleLimits["StationEngineer"] = 3;
        set.DefaultRoleLimits["Captain"] = 1;
        store.SaveSet(set);

        var loaded = store.LoadSet(setId);
        var json = ctx.UserData.ReadAllText(store.SetPath(setId));

        Assert.Multiple(() =>
        {
            Assert.That(loaded.AutosaveMinutes, Is.EqualTo(30));
            Assert.That(loaded.AutosaveWarningMinutes, Is.EqualTo(2));
            Assert.That(loaded.BaseMapPath, Is.EqualTo("/Maps/saltern.yml"));
            Assert.That(loaded.DefaultRoleAliases["Passenger"], Is.EqualTo("Поселенец"));
            Assert.That(loaded.DefaultRoleLimits["Passenger"], Is.EqualTo(20));
            Assert.That(loaded.DefaultRoleLimits["StationEngineer"], Is.EqualTo(3));
            Assert.That(loaded.DefaultRoleLimits["Captain"], Is.EqualTo(1));
            Assert.That(json, Does.Contain("Поселенец"));
            Assert.That(SetDirectoryEntries(ctx.UserData, store, setId), Has.None.StartsWith(".tmp-"));
            Assert.That(SetDirectoryEntries(ctx.UserData, store, setId), Has.None.StartsWith(".backup-"));
        });
    }

    [Test]
    public void CampaignConfigPersistsWeeklyRuntimeSettings()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;
        const string setId = "season-runtime";
        var set = store.CreateSet(setId, "Packed", 30, 8, baseMapPath: "/Maps/saltern.yml");

        set.PersistAutonomousMobs = false;
        set.PersistPlayerControlledBorgs = true;
        set.ExcludedMobPrototypes.Remove("MobMouse");
        set.ExcludedMobPrototypes.Add("MobHamster");
        set.MinPlaytimeHours = 10;
        set.DiscordChannel = "#weekly-event";
        set.RandomGameRulesEnabled = false;
        set.WeeklyTechnologies.Add(new WeeklyTechnologyEntry
        {
            TechnologyId = "AdvancedTools",
            Branch = "industrial",
            Cost = 7500,
            Tier = 1,
            RecipeIds = { "PowerDrillRecipe", "AdvancedWelderRecipe" },
        });
        set.WeeklyCargoProducts.Add(new WeeklyCargoProductEntry
        {
            ProductId = "SteelOrder",
            Category = "Resources",
            Cost = 1500,
            Boxed = true,
            Amount = 30,
            ItemPrototype = "SheetSteel",
        });

        store.SaveSet(set);

        var loaded = store.LoadSet(setId);
        var json = ctx.UserData.ReadAllText(store.SetPath(setId));

        Assert.Multiple(() =>
        {
            Assert.That(loaded.PersistAutonomousMobs, Is.False);
            Assert.That(loaded.PersistPlayerControlledBorgs, Is.True);
            Assert.That(loaded.ExcludedMobPrototypes, Does.Not.Contain("MobMouse"));
            Assert.That(loaded.ExcludedMobPrototypes, Contains.Item("MobHamster"));
            Assert.That(loaded.MinPlaytimeHours, Is.EqualTo(10));
            Assert.That(loaded.DiscordChannel, Is.EqualTo("#weekly-event"));
            Assert.That(loaded.RandomGameRulesEnabled, Is.False);
            Assert.That(loaded.WeeklyTechnologies, Has.Count.EqualTo(1));
            Assert.That(loaded.WeeklyTechnologies[0].TechnologyId, Is.EqualTo("AdvancedTools"));
            Assert.That(loaded.WeeklyTechnologies[0].RecipeIds, Is.EqualTo(new[] { "PowerDrillRecipe", "AdvancedWelderRecipe" }));
            Assert.That(loaded.WeeklyCargoProducts, Has.Count.EqualTo(1));
            Assert.That(loaded.WeeklyCargoProducts[0].ProductId, Is.EqualTo("SteelOrder"));
            Assert.That(loaded.WeeklyCargoProducts[0].Amount, Is.EqualTo(30));
            Assert.That(json, Does.Contain("\"PersistAutonomousMobs\""));
            Assert.That(json, Does.Contain("\"WeeklyTechnologies\""));
            Assert.That(json, Does.Contain("\"WeeklyCargoProducts\""));
            Assert.That(SetDirectoryEntries(ctx.UserData, store, setId), Has.None.StartsWith(".tmp-"));
            Assert.That(SetDirectoryEntries(ctx.UserData, store, setId), Has.None.StartsWith(".backup-"));
        });
    }

    [Test]
    public void CampaignConfigPersistsForcedRoleAssignments()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;
        const string setId = "season-forced-roles";
        var set = store.CreateSet(setId, "Packed", 30, 8, baseMapPath: "/Maps/saltern.yml");
        var userId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 06, 16, 12, 30, 00, DateTimeKind.Utc);

        set.ForcedRoleAssignments.Add(new WeeklyForcedRoleAssignment
        {
            PlayerNetUserId = userId.ToString(),
            LastKnownCKey = "forcedcaptain",
            JobId = "Captain",
            BypassPlaytime = true,
            CreatedBy = "integration-test",
            CreatedAt = createdAt,
        });

        store.SaveSet(set);

        var loaded = store.LoadSet(setId);
        var json = ctx.UserData.ReadAllText(store.SetPath(setId));

        Assert.Multiple(() =>
        {
            Assert.That(loaded.ForcedRoleAssignments, Has.Count.EqualTo(1));
            Assert.That(loaded.ForcedRoleAssignments[0].PlayerNetUserId, Is.EqualTo(userId.ToString()));
            Assert.That(loaded.ForcedRoleAssignments[0].LastKnownCKey, Is.EqualTo("forcedcaptain"));
            Assert.That(loaded.ForcedRoleAssignments[0].JobId, Is.EqualTo("Captain"));
            Assert.That(loaded.ForcedRoleAssignments[0].BypassPlaytime, Is.True);
            Assert.That(loaded.ForcedRoleAssignments[0].CreatedBy, Is.EqualTo("integration-test"));
            Assert.That(loaded.ForcedRoleAssignments[0].CreatedAt, Is.EqualTo(createdAt));
            Assert.That(json, Does.Contain("\"forcedRoleAssignments\""));
            Assert.That(json, Does.Contain("\"playerNetUserId\""));
            Assert.That(json, Does.Contain("\"bypassPlaytime\""));
            Assert.That(json, Does.Not.Contain("\"ForcedRoleAssignments\""));
        });
    }

    [Test]
    public void WeeklyRecipesConfigPersistsAsSeparateJson()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;
        const string setId = "season-recipes";
        store.CreateSet(setId, "Packed", 30, 8, baseMapPath: "/Maps/saltern.yml");

        var config = new WeeklyRecipesConfig
        {
            Recipes =
            {
                new WeeklyRecipeDefinition
                {
                    Id = "DeathSquadArmorRecipe",
                    ResultPrototype = "ClothingOuterHardsuitDeathsquad",
                    ResultAmount = 1,
                    ProductionTimeSeconds = 30,
                    LatheTargets = { "security" },
                    Materials =
                    {
                        ["Steel"] = 2000,
                        ["Plasteel"] = 6000,
                        ["Durathread"] = 3000,
                    },
                    TechnologyIds = { "DeathSquadEquipment" },
                },
            },
        };

        store.SaveRecipes(setId, config);

        Assert.That(store.TryLoadRecipes(setId, out var loaded), Is.True);
        var json = ctx.UserData.ReadAllText(store.RecipesPath(setId));

        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Recipes, Has.Count.EqualTo(1));
            Assert.That(loaded.Recipes[0].Id, Is.EqualTo("DeathSquadArmorRecipe"));
            Assert.That(loaded.Recipes[0].Materials["Plasteel"], Is.EqualTo(6000));
            Assert.That(json, Does.Contain("\"schemaVersion\""));
            Assert.That(json, Does.Contain("\"recipes\""));
            Assert.That(json, Does.Contain("DeathSquadArmorRecipe"));
            Assert.That(SetDirectoryEntries(ctx.UserData, store, setId), Has.None.StartsWith(".tmp-"));
            Assert.That(SetDirectoryEntries(ctx.UserData, store, setId), Has.None.StartsWith(".backup-"));
        });
    }

    [Test]
    public void SnapshotRoleOverridesPersistRoleLimits()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;
        const string setId = "season-01";
        const string snapshotId = "manual-20260614-100002-limits";
        var temp = store.TempSnapshotDirectory(setId, snapshotId);

        WriteFullBundle(store, ctx.UserData, temp, setId, snapshotId, "role limits");
        store.SaveRoleOverrides(temp, new WeeklyRoleOverrides
        {
            DisabledJobs = { "Botanist" },
            RoleAliases = { ["Passenger"] = "Поселенец" },
            RoleLimits =
            {
                ["Passenger"] = 20,
                ["Captain"] = 1,
            },
        });
        store.ReplaceSnapshotDirectory(temp, setId, snapshotId);

        Assert.Multiple(() =>
        {
            Assert.That(store.TryLoadRoleOverrides(setId, snapshotId, out var overrides), Is.True);
            Assert.That(overrides!.DisabledJobs, Contains.Item("Botanist"));
            Assert.That(overrides.RoleAliases["Passenger"], Is.EqualTo("Поселенец"));
            Assert.That(overrides.RoleLimits["Passenger"], Is.EqualTo(20));
            Assert.That(overrides.RoleLimits["Captain"], Is.EqualTo(1));
        });
    }

    [Test]
    public void ExistingSnapshotIsSafelyReplaced()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;
        const string setId = "season-01";
        const string snapshotId = "manual-20260614-100003";
        var final = store.SnapshotDirectory(setId, snapshotId);
        var temp = store.TempSnapshotDirectory(setId, snapshotId);

        WriteFullBundle(store, ctx.UserData, final, setId, snapshotId, "old bundle");
        WriteFullBundle(store, ctx.UserData, temp, setId, snapshotId, "new bundle");

        store.ReplaceSnapshotDirectory(temp, setId, snapshotId);

        Assert.Multiple(() =>
        {
            Assert.That(store.TryLoadSnapshot(setId, snapshotId, out var metadata), Is.True);
            Assert.That(metadata!.Notes, Is.EqualTo("new bundle"));
            Assert.That(SnapshotDirectoryEntries(ctx.UserData, store, setId), Has.None.StartsWith(".backup-"));
        });
    }

    [Test]
    public void ExistingSnapshotIsRestoredWhenReplacementFails()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;
        const string setId = "season-01";
        const string snapshotId = "manual-20260614-100004";
        var final = store.SnapshotDirectory(setId, snapshotId);
        var temp = store.TempSnapshotDirectory(setId, snapshotId);

        WriteFullBundle(store, ctx.UserData, final, setId, snapshotId, "old bundle");
        WriteFullBundle(store, ctx.UserData, temp, setId, snapshotId, "new bundle");

        Assert.Throws<IOException>(() =>
            store.ReplaceSnapshotDirectoryForTest(temp, setId, snapshotId, () =>
                ctx.UserData.WriteAllText(final, "block destination move")));

        Assert.Multiple(() =>
        {
            Assert.That(store.TryLoadSnapshot(setId, snapshotId, out var metadata), Is.True);
            Assert.That(metadata!.Notes, Is.EqualTo("old bundle"));
            Assert.That(ctx.UserData.Exists(temp), Is.True);
            Assert.That(SnapshotDirectoryEntries(ctx.UserData, store, setId), Has.None.StartsWith(".backup-"));
        });
    }

    [Test]
    public void SuccessfulReplacementRemovesTempDirectories()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;
        const string setId = "season-01";
        const string snapshotId = "manual-20260614-100005";
        var temp = store.TempSnapshotDirectory(setId, snapshotId);

        WriteFullBundle(store, ctx.UserData, temp, setId, snapshotId, "no temp");
        store.ReplaceSnapshotDirectory(temp, setId, snapshotId);

        Assert.That(SnapshotDirectoryEntries(ctx.UserData, store, setId), Has.None.StartsWith(".tmp-"));
    }

    [Test]
    public void SnapshotDirectoryReplacementRejectsPathTraversal()
    {
        using var ctx = CreateContext();
        var store = ctx.Store;

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() =>
                store.ReplaceSnapshotDirectory(new ResPath("/weekly-mode/sets/season-01/snapshots/../escape"), "season-01", "manual-1"));
            Assert.Throws<InvalidOperationException>(() =>
                store.ReplaceSnapshotDirectory(new ResPath("relative/tmp"), "season-01", "manual-1"));

            if (OperatingSystem.IsWindows())
            {
                Assert.Throws<InvalidOperationException>(() =>
                    store.ReplaceSnapshotDirectory(new ResPath("/C:/outside"), "season-01", "manual-1"));
            }
        });
    }

    private static WeeklyModeStoreTestContext CreateContext()
    {
        var root = Path.Combine(Path.GetTempPath(), "ss14-weekly-store-tests", Guid.NewGuid().ToString("N"));
        var userData = new TestWritableDirProvider(root);
        var resource = new Mock<IResourceManager>(MockBehavior.Strict);
        resource.SetupGet(x => x.UserData).Returns(userData);
        return new WeeklyModeStoreTestContext(root, userData, new WeeklyModeStore(resource.Object, "/weekly-mode"));
    }

    private static void WriteFullBundle(
        WeeklyModeStore store,
        IWritableDirProvider userData,
        ResPath directory,
        string setId,
        string snapshotId,
        string note)
    {
        userData.CreateDir(directory);
        userData.WriteAllText(store.SnapshotFilePath(directory, WeeklyModeStore.StationFileName), "station");
        store.SaveRoleOverrides(directory, new WeeklyRoleOverrides());
        store.SaveContainerPatch(directory, new WeeklyContainerPatch());
        store.SaveSnapshotIntegrity(directory, new WeeklySnapshotIntegrity
        {
            SetId = setId,
            SnapshotId = snapshotId,
            CreatedAtUtc = DateTime.UtcNow,
            Files =
            {
                [WeeklyModeStore.StationFileName] = new WeeklySnapshotFileIntegrity
                {
                    SizeBytes = 7,
                    Sha256 = "test",
                }
            },
        });
        store.SaveSnapshotMetadata(directory, new WeeklySnapshotMetadata
        {
            SetId = setId,
            SnapshotId = snapshotId,
            Kind = WeeklySnapshotKind.Manual,
            BaseMapPrototype = "Packed",
            BaseMapPath = "/Maps/packed.yml",
            CreatedAtUtc = DateTime.UtcNow,
            Notes = note,
            CreatedBy = "test",
            BundleSizeBytes = 1,
            SavedMapId = 7,
            SavedMapName = "Packed",
            SavedGridIds = { 8, 9 },
            EntityCount = 42,
        });
    }

    private static MappingDataNode SerializedMapData(string? mapName, params MappingDataNode[] extraComponents)
    {
        var meta = new MappingDataNode
        {
            { "type", new ValueDataNode("MetaData") },
        };

        if (mapName != null)
            meta.Add("name", new ValueDataNode(mapName));

        var components = new SequenceDataNode(
            meta,
            new MappingDataNode
            {
                { "type", new ValueDataNode("Map") },
            });
        foreach (var component in extraComponents)
            components.Add(component);

        return new MappingDataNode
        {
            { "maps", new SequenceDataNode(new ValueDataNode("1")) },
            {
                "entities",
                new SequenceDataNode(
                    new MappingDataNode
                    {
                        { "proto", new ValueDataNode(string.Empty) },
                        {
                            "entities",
                            new SequenceDataNode(
                                new MappingDataNode
                                {
                                    { "uid", new ValueDataNode("1") },
                                    { "components", components },
                                })
                        },
                    })
            },
        };
    }

    private static SequenceDataNode SerializedComponents(MappingDataNode data)
    {
        return SerializedEntity(data)
            .Get<SequenceDataNode>("components");
    }

    private static MappingDataNode SerializedEntity(MappingDataNode data)
    {
        return data.Get<SequenceDataNode>("entities")
            .Cast<MappingDataNode>(0)
            .Get<SequenceDataNode>("entities")
            .Cast<MappingDataNode>(0);
    }

    private static string ComponentType(MappingDataNode component)
    {
        return component.Get<ValueDataNode>("type").Value;
    }

    private static string[] SnapshotDirectoryEntries(IWritableDirProvider userData, WeeklyModeStore store, string setId)
    {
        return userData.DirectoryEntries(store.SnapshotsDirectory(setId)).ToArray();
    }

    private static string[] SetDirectoryEntries(IWritableDirProvider userData, WeeklyModeStore store, string setId)
    {
        return userData.DirectoryEntries(store.SetDirectory(setId)).ToArray();
    }

    private sealed class WeeklyModeStoreTestContext : IDisposable
    {
        public WeeklyModeStoreTestContext(string root, TestWritableDirProvider userData, WeeklyModeStore store)
        {
            Root = root;
            UserData = userData;
            Store = store;
        }

        public string Root { get; }
        public TestWritableDirProvider UserData { get; }
        public WeeklyModeStore Store { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }

    private sealed class TestWritableDirProvider : IWritableDirProvider
    {
        public TestWritableDirProvider(string rootDir)
        {
            RootDir = Path.EndsInDirectorySeparator(rootDir)
                ? Path.GetFullPath(rootDir)
                : Path.GetFullPath(rootDir) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(RootDir);
        }

        public string RootDir { get; }

        public void CreateDir(ResPath path)
        {
            Directory.CreateDirectory(GetFullPath(path));
        }

        public void Delete(ResPath path)
        {
            var fullPath = GetFullPath(path);
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
            else if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        public bool Exists(ResPath path)
        {
            var fullPath = GetFullPath(path);
            return Directory.Exists(fullPath) || File.Exists(fullPath);
        }

        public (IEnumerable<ResPath> files, IEnumerable<ResPath> directories) Find(string pattern, bool recursive = true)
        {
            throw new NotSupportedException();
        }

        public IEnumerable<string> DirectoryEntries(ResPath path)
        {
            var fullPath = GetFullPath(path);
            if (!Directory.Exists(fullPath))
                yield break;

            foreach (var entry in Directory.EnumerateFileSystemEntries(fullPath))
                yield return Path.GetRelativePath(fullPath, entry);
        }

        public bool IsDir(ResPath path)
        {
            return Directory.Exists(GetFullPath(path));
        }

        public Stream Open(ResPath path, FileMode fileMode, FileAccess access, FileShare share)
        {
            return File.Open(GetFullPath(path), fileMode, access, share);
        }

        public IWritableDirProvider OpenSubdirectory(ResPath path)
        {
            return new TestWritableDirProvider(GetFullPath(path));
        }

        public void Rename(ResPath oldPath, ResPath newPath)
        {
            var oldFullPath = GetFullPath(oldPath);
            var newFullPath = GetFullPath(newPath);

            if (Directory.Exists(oldFullPath))
            {
                Directory.Move(oldFullPath, newFullPath);
                return;
            }

            File.Move(oldFullPath, newFullPath);
        }

        public void OpenOsWindow(ResPath path)
        {
            throw new NotSupportedException();
        }

        private string GetFullPath(ResPath path)
        {
            if (!path.IsRooted)
                throw new ArgumentException($"Path must be rooted: {path}");

            var relativePath = path.ToRelativeSystemPath();
            if (Path.IsPathRooted(relativePath) || Path.IsPathFullyQualified(relativePath))
                throw new InvalidOperationException($"Path resolves outside test root: {path}");

            var fullPath = Path.GetFullPath(Path.Combine(RootDir, relativePath));
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!fullPath.StartsWith(RootDir, comparison))
                throw new InvalidOperationException($"Path resolves outside test root: {path}");

            return fullPath;
        }
    }
}
