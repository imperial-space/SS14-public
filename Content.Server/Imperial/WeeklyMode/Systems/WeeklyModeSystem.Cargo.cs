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
    internal WeeklyCargoSettingsComponent GetCargoSettings()
    {
        return TryComp<WeeklyCargoSettingsComponent>(_activeWeeklyMapEntity, out var settings)
            ? settings
            : new WeeklyCargoSettingsComponent();
    }

    public bool TryAddCargoProduct(
        string setId,
        string productId,
        string category,
        int cost,
        bool boxed,
        int amount,
        string itemPrototype,
        out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);

        productId = productId.Trim();
        if (!WeeklyModeStore.IsSafeId(productId))
        {
            message = "productId must contain only ASCII letters, digits, '-', '_' or '.'.";
            return false;
        }

        if (set.WeeklyCargoProducts.Any(entry => string.Equals(entry.ProductId, productId, StringComparison.Ordinal)))
        {
            message = $"Cargo product '{productId}' already exists in weekly set '{set.SetId}'.";
            return false;
        }

        category = category.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(category))
        {
            message = "Cargo category may not be empty.";
            return false;
        }

        if (cost < 0)
        {
            message = "Cargo product cost may not be negative.";
            return false;
        }

        if (amount < 1)
        {
            message = "Cargo product amount must be at least 1.";
            return false;
        }

        itemPrototype = itemPrototype.Trim();
        if (!_prototype.TryIndex<EntityPrototype>(itemPrototype, out _))
        {
            message = $"Unknown item entity prototype '{itemPrototype}'.";
            return false;
        }

        if (boxed && amount > GetCargoSettings().BoxedCapacity)
        {
            message = $"Boxed weekly cargo products may contain at most {GetCargoSettings().BoxedCapacity} entities.";
            return false;
        }

        set.WeeklyCargoProducts.Add(new WeeklyCargoProductEntry
        {
            ProductId = productId,
            Category = category,
            Cost = cost,
            Boxed = boxed,
            Amount = amount,
            ItemPrototype = itemPrototype,
        });

        set.WeeklyCargoProducts.Sort((a, b) => string.Compare(a.ProductId, b.ProductId, StringComparison.Ordinal));

        if (!TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Cargo, out message))
            return false;

        message = $"Added weekly cargo product '{productId}' to category '{category}' in set '{set.SetId}'.";
        return true;
    }

    public bool TryUpdateCargoProduct(
        string setId,
        string productId,
        string category,
        int cost,
        bool boxed,
        int amount,
        string itemPrototype,
        out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);

        productId = productId.Trim();
        var entry = set.WeeklyCargoProducts.FirstOrDefault(entry =>
            string.Equals(entry.ProductId, productId, StringComparison.Ordinal));
        if (entry == null)
        {
            message = $"Weekly set '{set.SetId}' has no cargo product '{productId}'.";
            return false;
        }

        category = category.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(category))
        {
            message = "Cargo category may not be empty.";
            return false;
        }

        if (cost < 0)
        {
            message = "Cargo product cost may not be negative.";
            return false;
        }

        if (amount < 1)
        {
            message = "Cargo product amount must be at least 1.";
            return false;
        }

        itemPrototype = itemPrototype.Trim();
        if (!_prototype.TryIndex<EntityPrototype>(itemPrototype, out _))
        {
            message = $"Unknown item entity prototype '{itemPrototype}'.";
            return false;
        }

        if (boxed && amount > GetCargoSettings().BoxedCapacity)
        {
            message = $"Boxed weekly cargo products may contain at most {GetCargoSettings().BoxedCapacity} entities.";
            return false;
        }

        entry.Category = category;
        entry.Cost = cost;
        entry.Boxed = boxed;
        entry.Amount = amount;
        entry.ItemPrototype = itemPrototype;
        set.WeeklyCargoProducts.Sort((a, b) => string.Compare(a.ProductId, b.ProductId, StringComparison.Ordinal));

        if (!TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Cargo, out message))
            return false;

        message = $"Updated weekly cargo product '{productId}' in set '{set.SetId}'.";
        return true;
    }

    public bool TryRemoveCargoProduct(string setId, string productId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);

        var removed = set.WeeklyCargoProducts.RemoveAll(entry => string.Equals(entry.ProductId, productId, StringComparison.Ordinal));

        if (removed > 0 && !TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Cargo, out message))
            return false;

        message = removed == 0
            ? $"Weekly set '{set.SetId}' had no cargo product '{productId}'."
            : $"Removed weekly cargo product '{productId}' from set '{set.SetId}'.";
        return true;
    }

    public string ListCargoProducts(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        if (set.WeeklyCargoProducts.Count == 0)
            return $"Weekly set '{set.SetId}' has an empty cargo catalog.";

        var lines = new List<string> { $"Weekly cargo catalog for '{set.SetId}':" };
        foreach (var entry in set.WeeklyCargoProducts
                     .OrderBy(x => x.Category, StringComparer.Ordinal)
                     .ThenBy(x => x.ProductId, StringComparer.Ordinal))
        {
            lines.Add($"- {entry.Category}: {entry.ProductId} cost={entry.Cost} boxed={entry.Boxed} amount={entry.Amount} item={entry.ItemPrototype}");
        }

        return string.Join('\n', lines);
    }

    public bool TryClearCargoProducts(string setId, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);
        var removed = set.WeeklyCargoProducts.Count;
        set.WeeklyCargoProducts.Clear();

        if (!TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Cargo, out message))
            return false;

        message = $"Cleared {removed} weekly cargo products from set '{set.SetId}'.";
        return true;
    }

    public bool TryClearCargoCategory(string setId, string category, out string message)
    {
        if (!TryLoadConfigEditableSet(setId, out var set, out message))
            return false;

        var originalSet = CloneSet(set);
        category = category.Trim().Trim('"');
        var removed = set.WeeklyCargoProducts.RemoveAll(entry => string.Equals(entry.Category, category, StringComparison.Ordinal));

        if (removed > 0 && !TryCommitConfigChange(originalSet, set, WeeklyLiveConfigChange.Cargo, out message))
            return false;

        message = $"Cleared {removed} weekly cargo products from category '{category}' in set '{set.SetId}'.";
        return true;
    }

    public string ValidateCargoProducts(string setId)
    {
        if (!_store.TryLoadSet(setId, out var set))
            return $"Weekly set '{setId}' was not found.";

        var errors = ValidateWeeklyCargoProducts(set).ToList();
        return errors.Count == 0
            ? $"Weekly cargo catalog for '{set.SetId}' is valid."
            : $"Weekly cargo catalog for '{set.SetId}' is invalid:\n- {string.Join("\n- ", errors)}";
    }
}
