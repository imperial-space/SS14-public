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
    [Flags]
    private enum WeeklyLiveConfigChange
    {
        None = 0,
        Research = 1,
        Cargo = 2,
        Recipes = 4,
        Roles = 8,
    }

    private sealed record RollbackConfirmation(string SetId, string SnapshotId, DateTime ExpiresAtUtc, string RequestedBy);
    internal readonly record struct SnapshotSanitizationResult(
        int RemovedStationMembers,
        int RemovedSuitSensorEntityReferences,
        int RemovedInvalidContainerReferences,
        int RemovedStaleBuckleReferences,
        int RemovedStaleStrapReferences,
        int RemovedStaleJointReferences,
        int DisabledBorgChassis,
        int ResetMapInitializationFields,
        int ResetMindContainers,
        int SuppressedMapInitOnlyComponents,
        int SuppressedStartingItems)
    {
        public bool Changed => RemovedStationMembers > 0 ||
                               RemovedSuitSensorEntityReferences > 0 ||
                               RemovedInvalidContainerReferences > 0 ||
                               RemovedStaleBuckleReferences > 0 ||
                               RemovedStaleStrapReferences > 0 ||
                               RemovedStaleJointReferences > 0 ||
                               DisabledBorgChassis > 0 ||
                               ResetMapInitializationFields > 0 ||
                               ResetMindContainers > 0 ||
                               SuppressedMapInitOnlyComponents > 0 ||
                               SuppressedStartingItems > 0;
    }

    internal readonly record struct SnapshotExclusionResult(
        HashSet<EntityUid> ExcludedRoots,
        HashSet<string> ForceMapSavablePrototypes,
        int AutonomousMobsIncluded,
        int AutonomousMobsExcluded,
        int PlayerBodiesExcluded,
        int BorgChassisIncluded,
        int BorgChassisExcluded);

}
