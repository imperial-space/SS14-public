using Content.Server.WeeklyMode.Components;
using Content.Shared.Imperial.ICCVar;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Cargo.Systems;
using Content.Server.Chemistry.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Light.EntitySystems;
using Content.Server.Maps;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Research.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Actions;
using Content.Shared.CartridgeLoader;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chat;
using Content.Shared.Containers;
using Content.Shared.Containers.ItemSlots;
using Content.Server.WeeklyMode.Storage;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Light.Components;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Maps;
using Content.Shared.Materials;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.VendingMachines;
using Content.Shared.WeeklyMode;
using Robust.Server.Containers;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server.WeeklyMode.Systems;

public sealed partial class WeeklyModeSystem
{
    private bool TrySaveMapExcludingPlayers(
        MapId mapId,
        ResPath path,
        SnapshotExclusionResult snapshotExclusions,
        [NotNullWhen(true)] out Dictionary<EntityUid, int>? yamlUidMap)
    {
        yamlUidMap = null;
        var excludedRoots = snapshotExclusions.ExcludedRoots;
        void Filter(Entity<MetaDataComponent> ent, ref bool serializable)
        {
            if (!serializable)
                return;

            if (IsEntityOrParentExcluded(ent.Owner, excludedRoots))
                serializable = false;
        }

        var options = SerializationOptions.Default with
        {
            MissingEntityBehaviour = MissingEntityBehaviour.Ignore,
        };

        if (!_map.TryGetMap(mapId, out var mapUid))
        {
            _sawmill.Error($"Unable to find map {mapId} while saving weekly snapshot.");
            return false;
        }

        options.Category = FileCategory.Map;
        var serializer = new EntitySerializer(_dependency, options);
        serializer.OnIsSerializeable += Filter;
        var forcedMapSavable = new Dictionary<EntityPrototype, bool>();
        try
        {
            forcedMapSavable = ForceMapSavablePrototypes(snapshotExclusions.ForceMapSavablePrototypes);

            var roots = new HashSet<EntityUid> { mapUid.Value };
            var ev = new BeforeSerializationEvent(roots, new HashSet<MapId> { mapId }, FileCategory.Map);
            RaiseLocalEvent(ev);

            serializer.SerializeEntityRecursive(roots);
            var data = serializer.Write();
            var category = serializer.GetCategory();
            if (category != FileCategory.Map)
            {
                _sawmill.Error($"Failed to save weekly snapshot map {mapId} as a map. Output: {category}");
                return false;
            }

            WriteSerializedSnapshot(path, data);

            var ev2 = new AfterSerializationEvent(roots, data, category);
            RaiseLocalEvent(ev2);

            yamlUidMap = new Dictionary<EntityUid, int>(serializer.YamlUidMap);
            return true;
        }
        catch (Exception e)
        {
            _sawmill.Error($"Caught exception while trying to serialize weekly snapshot map {mapId}:\n{e}");
            return false;
        }
        finally
        {
            RestoreMapSavablePrototypes(forcedMapSavable);
            serializer.OnIsSerializeable -= Filter;
        }
    }

    private bool TrySanitizeSerializedSnapshot(
        ResPath stationPath,
        out SnapshotSanitizationResult result,
        out string message)
    {
        result = default;

        if (!_mapLoader.TryReadFile(stationPath, out var data))
        {
            message = $"Failed to read serialized snapshot map '{stationPath}' for sanitation.";
            return false;
        }

        result = SanitizeSerializedSnapshot(data, GetInheritedSnapshotMapInitOnlyComponents);
        if (result.Changed)
            WriteSerializedSnapshot(stationPath, data);

        message = string.Empty;
        return true;
    }

    internal static SnapshotSanitizationResult SanitizeSerializedSnapshot(MappingDataNode data)
    {
        return SanitizeSerializedSnapshot(data, null);
    }

    private static SnapshotSanitizationResult SanitizeSerializedSnapshot(
        MappingDataNode data,
        Func<string, IReadOnlyList<string>>? getInheritedMapInitOnlyComponents)
    {
        var removedStationMembers = 0;
        var removedSuitSensorReferences = 0;
        var removedInvalidContainerReferences = 0;
        var removedStaleBuckleReferences = 0;
        var removedStaleStrapReferences = 0;
        var removedStaleJointReferences = 0;
        var disabledBorgChassis = 0;
        var resetMapInitializationFields = 0;
        var resetMindContainers = 0;
        var suppressedMapInitOnlyComponents = 0;
        var suppressedStartingItems = 0;

        if (!data.TryGet<SequenceDataNode>("entities", out var prototypeGroups))
        {
            return new SnapshotSanitizationResult(
                removedStationMembers,
                removedSuitSensorReferences,
                removedInvalidContainerReferences,
                removedStaleBuckleReferences,
                removedStaleStrapReferences,
                removedStaleJointReferences,
                disabledBorgChassis,
                resetMapInitializationFields,
                resetMindContainers,
                suppressedMapInitOnlyComponents,
                suppressedStartingItems);
        }

        foreach (var groupNode in prototypeGroups)
        {
            if (groupNode is not MappingDataNode group ||
                !group.TryGet<SequenceDataNode>("entities", out var entities))
            {
                continue;
            }

            var protoId = group.TryGet<ValueDataNode>("proto", out var protoNode)
                ? protoNode.Value ?? string.Empty
                : string.Empty;
            var inheritedMapInitOnlyComponents = getInheritedMapInitOnlyComponents?.Invoke(protoId)
                                                 ?? Array.Empty<string>();

            foreach (var entityNode in entities)
            {
                if (entityNode is not MappingDataNode entity)
                    continue;

                entity.TryGet<SequenceDataNode>("components", out var components);

                if (entity.Remove("mapInit"))
                    resetMapInitializationFields++;
                if (entity.Remove("paused"))
                    resetMapInitializationFields++;

                List<string>? missingMapInitOnlyComponents = null;
                if (inheritedMapInitOnlyComponents.Count > 0)
                    missingMapInitOnlyComponents = new List<string>(inheritedMapInitOnlyComponents);

                for (var i = (components?.Count ?? 0) - 1; i >= 0; i--)
                {
                    if (components![i] is not MappingDataNode component ||
                        !TryGetSerializedComponentType(component, out var type))
                    {
                        continue;
                    }

                    if (IsSnapshotMapInitOnlyComponent(type))
                    {
                        components.RemoveAt(i);
                        missingMapInitOnlyComponents ??= new List<string>();
                        missingMapInitOnlyComponents.Add(type);
                        continue;
                    }

                    switch (type)
                    {
                        case "StationMember":
                            components.RemoveAt(i);
                            removedStationMembers++;
                            break;
                        case "Actor":
                            components.RemoveAt(i);
                            resetMindContainers++;
                            break;
                        case "MindContainer":
                            resetMindContainers += ResetSerializedMindContainer(component);
                            break;
                        case "BorgChassis":
                            disabledBorgChassis += DisableSerializedBorgChassis(component);
                            break;
                        case "SuitSensor":
                            if (component.Remove("station"))
                                removedSuitSensorReferences++;
                            if (component.Remove("user"))
                                removedSuitSensorReferences++;
                            break;
                        case "ContainerContainer":
                            removedInvalidContainerReferences += RemoveInvalidContainerReferences(component);
                            break;
                        case "Buckle":
                            removedStaleBuckleReferences += RemoveStaleBuckleReferences(component);
                            break;
                        case "Strap":
                            removedStaleStrapReferences += RemoveStaleStrapReferences(component);
                            break;
                        case "Joint":
                            removedStaleJointReferences += RemoveStaleJointReferences(component);
                            break;
                        case "PoweredLight":
                            if (SuppressPoweredLightStartupLamp(component))
                                suppressedStartingItems++;
                            break;
                        case "ItemSlots":
                            suppressedStartingItems += SuppressSerializedStartingItems(component);
                            break;
                        case "ActionGrant":
                            suppressedStartingItems += SuppressActionGrantStartingActions(component);
                            break;
                        case "Bin":
                            suppressedStartingItems += SuppressSequenceField(component, "initialContents");
                            break;
                        case "CartridgeLoader":
                            suppressedStartingItems += SuppressSequenceField(component, "preinstalled");
                            break;
                        case "LightReplacer":
                            suppressedStartingItems += SuppressEntitySpawnEntries(component, "contents");
                            break;
                        case "VendingMachine":
                            if (SuppressVendingInitialRestock(component))
                                suppressedStartingItems++;
                            break;
                        case "Map":
                            if (component.Remove("mapInitialized"))
                                resetMapInitializationFields++;
                            if (component.Remove("mapPaused"))
                                resetMapInitializationFields++;
                            break;
                    }
                }

                if (missingMapInitOnlyComponents is { Count: > 0 })
                {
                    suppressedMapInitOnlyComponents += AddMissingComponents(
                        entity,
                        missingMapInitOnlyComponents.Distinct(StringComparer.Ordinal));
                }
            }
        }

        return new SnapshotSanitizationResult(
            removedStationMembers,
            removedSuitSensorReferences,
            removedInvalidContainerReferences,
            removedStaleBuckleReferences,
            removedStaleStrapReferences,
            removedStaleJointReferences,
            disabledBorgChassis,
            resetMapInitializationFields,
            resetMindContainers,
            suppressedMapInitOnlyComponents,
            suppressedStartingItems);
    }

    private static int ResetSerializedMindContainer(MappingDataNode component)
    {
        var changed = 0;
        if (component.Remove("mind"))
            changed++;

        if (!component.TryGet<ValueDataNode>("hasMind", out var hasMind) ||
            !bool.TryParse(hasMind.Value, out var value) ||
            value)
        {
            component["hasMind"] = new ValueDataNode("false");
            changed++;
        }

        return changed;
    }

    private static int DisableSerializedBorgChassis(MappingDataNode component)
    {
        if (component.TryGet<ValueDataNode>("active", out var active) &&
            bool.TryParse(active.Value, out var value) &&
            !value)
        {
            return 0;
        }

        component["active"] = new ValueDataNode("false");
        return 1;
    }

    private static bool SuppressPoweredLightStartupLamp(MappingDataNode component)
    {
        if (component.TryGet<ValueDataNode>("hasLampOnSpawn", out var hasLampOnSpawn) &&
            hasLampOnSpawn.IsNull)
        {
            return false;
        }

        component["hasLampOnSpawn"] = ValueDataNode.Null();
        return true;
    }

    private static int SuppressSerializedStartingItems(DataNode node)
    {
        var suppressed = 0;
        switch (node)
        {
            case MappingDataNode mapping:
                foreach (var (key, value) in mapping.ToArray())
                {
                    if (key == "startingItem" &&
                        value is not ValueDataNode { IsNull: true })
                    {
                        mapping[key] = ValueDataNode.Null();
                        suppressed++;
                        continue;
                    }

                    suppressed += SuppressSerializedStartingItems(value);
                }

                break;
            case SequenceDataNode sequence:
                foreach (var value in sequence)
                    suppressed += SuppressSerializedStartingItems(value);
                break;
        }

        return suppressed;
    }

    private static int SuppressActionGrantStartingActions(MappingDataNode component)
    {
        return SuppressSequenceField(component, "actions", createWhenMissing: true);
    }

    private static bool SuppressVendingInitialRestock(MappingDataNode component)
    {
        if (component.TryGet<ValueDataNode>("suppressInitialRestock", out var suppressed) &&
            bool.TryParse(suppressed.Value, out var value) &&
            value)
        {
            return false;
        }

        component["suppressInitialRestock"] = new ValueDataNode("true");
        return true;
    }

    private static int SuppressSequenceField(
        MappingDataNode component,
        string field,
        bool createWhenMissing = false)
    {
        if (component.TryGet<SequenceDataNode>(field, out var sequence))
        {
            var suppressed = sequence.Count;
            sequence.Clear();
            return suppressed;
        }

        if (!createWhenMissing)
            return 0;

        component[field] = new SequenceDataNode();
        return 1;
    }

    private static int SuppressEntitySpawnEntries(MappingDataNode component, string field)
    {
        if (!component.TryGet<SequenceDataNode>(field, out var sequence))
            return 0;

        var suppressed = 0;
        foreach (var entry in sequence.OfType<MappingDataNode>())
        {
            if (entry.TryGet<ValueDataNode>("amount", out var amount) &&
                int.TryParse(amount.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                suppressed += Math.Max(parsed, 0);
            }
            else
            {
                suppressed++;
            }
        }

        sequence.Clear();
        return suppressed;
    }

    private IReadOnlyList<string> GetInheritedSnapshotMapInitOnlyComponents(string protoId)
    {
        if (string.IsNullOrWhiteSpace(protoId) ||
            !_prototype.TryIndex<EntityPrototype>(protoId, out var proto))
        {
            return Array.Empty<string>();
        }

        List<string>? result = null;
        foreach (var componentName in SnapshotMapInitOnlyComponents)
        {
            if (!proto.Components.ContainsKey(componentName))
                continue;

            result ??= new List<string>();
            result.Add(componentName);
        }

        return result != null ? result : Array.Empty<string>();
    }

    private static bool IsSnapshotMapInitOnlyComponent(string type)
    {
        return SnapshotMapInitOnlyComponents.Contains(type, StringComparer.Ordinal);
    }

    private static int AddMissingComponents(MappingDataNode entity, IEnumerable<string> componentNames)
    {
        if (!entity.TryGet<SequenceDataNode>("missingComponents", out var missingComponents))
        {
            missingComponents = new SequenceDataNode();
            entity.Add("missingComponents", missingComponents);
        }

        var existing = missingComponents
            .OfType<ValueDataNode>()
            .Select(node => node.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        var added = 0;
        foreach (var componentName in componentNames)
        {
            if (!existing.Add(componentName))
                continue;

            missingComponents.Add(new ValueDataNode(componentName));
            added++;
        }

        if (missingComponents.Count == 0)
            entity.Remove("missingComponents");

        return added;
    }

    private static bool TryGetSerializedComponentType(MappingDataNode component, [NotNullWhen(true)] out string? type)
    {
        type = null;
        if (!component.TryGet<ValueDataNode>("type", out var typeNode) ||
            string.IsNullOrWhiteSpace(typeNode.Value))
        {
            return false;
        }

        type = typeNode.Value;
        return true;
    }

    private static int RemoveInvalidContainerReferences(DataNode node)
    {
        var removed = 0;
        switch (node)
        {
            case MappingDataNode mapping:
                foreach (var (key, value) in mapping.ToArray())
                {
                    if (key == "ent" && IsInvalidEntityReference(value))
                    {
                        mapping[key] = ValueDataNode.Null();
                        removed++;
                        continue;
                    }

                    removed += RemoveInvalidContainerReferences(value);
                }

                break;
            case SequenceDataNode sequence:
                for (var i = sequence.Count - 1; i >= 0; i--)
                {
                    if (IsInvalidEntityReference(sequence[i]))
                    {
                        sequence.RemoveAt(i);
                        removed++;
                        continue;
                    }

                    removed += RemoveInvalidContainerReferences(sequence[i]);
                }

                break;
        }

        return removed;
    }

    private static int RemoveStaleBuckleReferences(MappingDataNode component)
    {
        if (!component.TryGet<ValueDataNode>("buckledTo", out var buckledTo) ||
            !IsInvalidEntityReference(buckledTo))
        {
            return 0;
        }

        component["buckledTo"] = ValueDataNode.Null();
        component.Remove("buckleTime");
        return 1;
    }

    private static int RemoveStaleStrapReferences(MappingDataNode component)
    {
        if (!component.TryGet<SequenceDataNode>("buckledEntities", out var buckledEntities))
            return 0;

        var removed = 0;
        for (var i = buckledEntities.Count - 1; i >= 0; i--)
        {
            if (!IsInvalidEntityReference(buckledEntities[i]))
                continue;

            buckledEntities.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    private static int RemoveStaleJointReferences(MappingDataNode component)
    {
        var removed = 0;
        if (component.TryGet<ValueDataNode>("relay", out var relay) &&
            IsInvalidEntityReference(relay))
        {
            component["relay"] = ValueDataNode.Null();
            removed++;
        }

        if (!component.TryGet<MappingDataNode>("joints", out var joints))
            return removed;

        foreach (var (jointId, jointData) in joints.ToArray())
        {
            if (!ContainsInvalidEntityReference(jointData))
                continue;

            joints.Remove(jointId);
            removed++;
        }

        return removed;
    }

    private static bool ContainsInvalidEntityReference(DataNode node)
    {
        if (IsInvalidEntityReference(node))
            return true;

        switch (node)
        {
            case MappingDataNode mapping:
                foreach (var (_, value) in mapping.ToArray())
                {
                    if (ContainsInvalidEntityReference(value))
                        return true;
                }

                return false;
            case SequenceDataNode sequence:
                return sequence.Any(ContainsInvalidEntityReference);
            default:
                return false;
        }
    }

    private static bool IsInvalidEntityReference(DataNode node)
    {
        return node is ValueDataNode { Value: "invalid" };
    }

    private void WriteSerializedSnapshot(ResPath path, MappingDataNode data)
    {
        path = path.ToRootedPath();
        _resource.UserData.CreateDir(path.Directory);
        using var writer = _resource.UserData.OpenWriteText(path);
        var document = new YamlDocument(data.ToYaml());
        var stream = new YamlStream { document };
        stream.Save(new YamlMappingFix(new Emitter(writer)), false);
    }

    private bool IsEntityOrParentExcluded(EntityUid uid, HashSet<EntityUid> excludedRoots)
    {
        if (excludedRoots.Count == 0)
            return false;

        var current = uid;
        var depth = 0;
        while (current.IsValid() && depth++ < 256)
        {
            if (excludedRoots.Contains(current))
                return true;

            if (!_xformQuery.TryGetComponent(current, out var xform))
                return false;

            current = xform.ParentUid;
        }

        return false;
    }
}
