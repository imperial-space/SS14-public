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
    public bool TrySetAutosave(string setId, int intervalMinutes, int warningMinutes, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (intervalMinutes < 1)
        {
            message = "Autosave interval must be at least 1 minute.";
            return false;
        }

        if (warningMinutes < 0)
        {
            message = "Autosave warning may not be negative.";
            return false;
        }

        if (warningMinutes >= intervalMinutes)
        {
            message = "Autosave warning must be shorter than the autosave interval.";
            return false;
        }

        set.AutosaveMinutes = intervalMinutes;
        set.AutosaveWarningMinutes = warningMinutes;
        _store.SaveSet(set);
        message = $"Autosave for weekly set '{set.SetId}' set to every {intervalMinutes} minutes with {warningMinutes} minute warning.";
        return true;
    }

    public bool TrySetPersistAutonomousMobs(string setId, bool enabled, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        set.PersistAutonomousMobs = enabled;
        _store.SaveSet(set);
        message = $"Autonomous mob persistence for weekly set '{set.SetId}' set to {enabled}.";
        return true;
    }

    public bool TryExcludeMobPrototype(string setId, string prototypeId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out _))
        {
            message = $"Unknown entity prototype '{prototypeId}'.";
            return false;
        }

        if (!set.ExcludedMobPrototypes.Contains(prototypeId, StringComparer.Ordinal))
            set.ExcludedMobPrototypes.Add(prototypeId);

        set.ExcludedMobPrototypes.Sort(StringComparer.Ordinal);
        _store.SaveSet(set);
        message = $"Excluded mob prototype '{prototypeId}' from weekly set '{set.SetId}'.";
        return true;
    }

    public string ListExcludedMobPrototypes(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        return set.ExcludedMobPrototypes.Count == 0
            ? $"Weekly set '{set.SetId}' has no excluded mob prototypes."
            : $"Excluded mob prototypes for weekly set '{set.SetId}':\n- {string.Join("\n- ", set.ExcludedMobPrototypes.OrderBy(x => x, StringComparer.Ordinal))}";
    }

    public bool TryClearExcludedMobPrototype(string setId, string prototypeId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (!set.ExcludedMobPrototypes.Remove(prototypeId))
        {
            message = $"Weekly set '{set.SetId}' had no excluded mob prototype '{prototypeId}'.";
            return true;
        }

        _store.SaveSet(set);
        message = $"Removed excluded mob prototype '{prototypeId}' from weekly set '{set.SetId}'.";
        return true;
    }

    public bool TrySetMinPlaytime(string setId, int hours, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        if (hours < 0)
        {
            message = "Minimum playtime may not be negative.";
            return false;
        }

        set.MinPlaytimeHours = hours;
        _store.SaveSet(set);
        message = $"Minimum server playtime for weekly set '{set.SetId}' set to {hours} hours.";
        return true;
    }

    public bool TrySetDiscordChannel(string setId, string channel, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        channel = channel.Trim().Trim('"');
        if (channel.Length > 128)
        {
            message = "Discord channel/link may not be longer than 128 characters.";
            return false;
        }

        if (channel.Any(char.IsControl))
        {
            message = "Discord channel/link may not contain control characters.";
            return false;
        }

        set.DiscordChannel = channel;
        _store.SaveSet(set);
        message = string.IsNullOrWhiteSpace(channel)
            ? $"Discord channel cleared for weekly set '{set.SetId}'."
            : $"Discord channel for weekly set '{set.SetId}' set to '{channel}'.";
        return true;
    }

    public string GetAccessStatus(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var discord = string.IsNullOrWhiteSpace(set.DiscordChannel) ? "<none>" : set.DiscordChannel;
        return $"Access for weekly set '{set.SetId}': minPlaytimeHours={set.MinPlaytimeHours}, discord={discord}.";
    }

    public bool TryClearAccess(string setId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        set.MinPlaytimeHours = 0;
        set.DiscordChannel = string.Empty;
        _store.SaveSet(set);
        message = $"Cleared access restrictions for weekly set '{set.SetId}'.";
        return true;
    }

    public bool TrySetRandomGameRules(string setId, bool enabled, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        set.RandomGameRulesEnabled = enabled;
        _store.SaveSet(set);
        message = $"Random gamerules for weekly set '{set.SetId}' set to {enabled}.";
        return true;
    }

    public string GetGameRulesStatus(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        return $"Gamerules for weekly set '{set.SetId}': randomGameRulesEnabled={set.RandomGameRulesEnabled}.";
    }
}
