using System.Linq;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Quirks;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Traits;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Quirks;

/// <summary>
/// SSquirks из TG: при появлении персонажа фильтрует выбранные причуды (filter_invalid_quirks)
/// и выдаёт их эффекты и предметы.
/// </summary>
public sealed partial class ImperialQuirkSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    public static readonly ProtoId<TraitCategoryPrototype> Category = "ImperialQuirks";

    /// <summary>CONFIG max_positive_quirks.</summary>
    private const int MaxPositiveQuirks = 6;

    /// <summary>CONFIG default_quirk_points: столько можно потратить сверх отрицательных причуд.</summary>
    private const int DefaultQuirkPoints = 2;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        InitializeNegative();
        InitializeFear();
        InitializeFood();
        InitializeNeutral();
        InitializePositive();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateNegative(frameTime);
        UpdateFear(frameTime);
        UpdateNeutral(frameTime);
        UpdatePositive(frameTime);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!HasComp<HumanoidProfileComponent>(args.Mob))
            return;

        var chosen = new List<TraitPrototype>();
        foreach (var id in args.Profile.TraitPreferences)
        {
            if (_proto.TryIndex(id, out var trait) && trait.Category == Category && _proto.HasIndex<ImperialQuirkPrototype>(id))
                chosen.Add(trait);
        }

        if (chosen.Count == 0)
            return;

        var accepted = FilterQuirks(chosen, out var removed);
        var holder = EnsureComp<ImperialQuirksComponent>(args.Mob);
        foreach (var trait in accepted)
        {
            ApplyQuirk(args.Mob, trait, args.Player);
            holder.Quirks.Add(trait.ID);
        }

        Dirty(args.Mob, holder);

        if (removed.Count > 0)
        {
            var names = string.Join(", ", removed.Select(t => Loc.GetString(t.Name)));
            Message(args.Player, Loc.GetString("imperial-quirk-removed", ("quirks", names)));
        }
    }

    /// <summary>filter_invalid_quirks: несовместимые, не больше 6 положительных, баланс не выше бесплатных очков.</summary>
    private List<TraitPrototype> FilterQuirks(List<TraitPrototype> quirks, out List<TraitPrototype> removed)
    {
        var accepted = new List<TraitPrototype>();
        var positive = new List<TraitPrototype>();
        removed = new List<TraitPrototype>();
        var balance = -DefaultQuirkPoints;

        foreach (var trait in quirks)
        {
            var quirk = _proto.Index<ImperialQuirkPrototype>(trait.ID);
            var blacklisted = accepted.Any(other => quirk.Incompatible.Contains(other.ID)
                || _proto.Index<ImperialQuirkPrototype>(other.ID).Incompatible.Contains(trait.ID));

            if (blacklisted || trait.Cost > 0 && positive.Count == MaxPositiveQuirks)
            {
                removed.Add(trait);
                continue;
            }

            if (trait.Cost > 0)
                positive.Add(trait);

            balance += trait.Cost;
            accepted.Add(trait);
        }

        if (balance <= 0)
            return accepted;

        // Лишние положительные снимаются по порядку выбора, пока баланс не сойдётся.
        foreach (var trait in positive)
        {
            balance -= trait.Cost;
            accepted.Remove(trait);
            removed.Add(trait);
            if (balance <= 0)
                break;
        }

        return accepted;
    }

    /// <summary>Выдать причуды без проверки баланса (админ-команды, тесты).</summary>
    public void ForceQuirks(EntityUid mob, IEnumerable<string> ids, ICommonSession? player = null)
    {
        var holder = EnsureComp<ImperialQuirksComponent>(mob);
        foreach (var id in ids)
        {
            var trait = _proto.Index<TraitPrototype>(id);
            ApplyQuirk(mob, trait, player);
            holder.Quirks.Add(trait.ID);
        }

        Dirty(mob, holder);
    }

    /// <summary>Фильтр TG для проверки: какие причуды останутся.</summary>
    public List<string> CheckFilter(IEnumerable<string> ids)
    {
        return FilterQuirks(ids.Select(id => _proto.Index<TraitPrototype>(id)).ToList(), out _).Select(t => t.ID).ToList();
    }

    private void ApplyQuirk(EntityUid mob, TraitPrototype trait, ICommonSession? player)
    {
        var quirk = _proto.Index<ImperialQuirkPrototype>(trait.ID);

        EntityManager.AddComponents(mob, quirk.Components, false);
        foreach (var special in quirk.Specials)
        {
            special.AfterEquip(mob);
        }

        foreach (var (slot, proto) in quirk.Equip)
        {
            var item = Spawn(proto, Transform(mob).Coordinates);
            if (_inventory.TryUnequip(mob, slot, out var old, true, true))
                GiveItem(mob, old.Value);

            if (!_inventory.TryEquip(mob, item, slot, true, true))
                GiveItem(mob, item);
        }

        foreach (var proto in quirk.Items)
        {
            GiveItem(mob, Spawn(proto, Transform(mob).Coordinates));
        }

        if (quirk.GainText is { } gain && player != null)
            Message(player, Loc.GetString(gain));

        ApplyTaste(mob, trait.ID);
        ApplyFear(mob, trait.ID, player);
        ApplyOther(mob, trait.ID, player);

        var ev = new ImperialQuirkAddedEvent(trait.ID, player);
        RaiseLocalEvent(mob, ref ev);
    }

    /// <summary>give_item_to_holder: рюкзак, руки, иначе под ноги.</summary>
    public void GiveItem(EntityUid mob, EntityUid item)
    {
        if (_inventory.TryGetSlotEntity(mob, "back", out var back)
            && TryComp<StorageComponent>(back, out var storage)
            && _storage.Insert(back.Value, item, out _, user: null, storage, playSound: false))
        {
            return;
        }

        _hands.PickupOrDrop(mob, item, animate: false);
    }

    public void Message(ICommonSession? player, string message)
    {
        if (player == null)
            return;

        _chatManager.ChatMessageToOne(ChatChannel.Server, FormattedMessage.RemoveMarkupOrThrow(message), message,
            default, false, player.Channel);
    }

    public void Message(EntityUid mob, string message)
    {
        if (TryComp<ActorComponent>(mob, out var actor))
            Message(actor.PlayerSession, message);
    }
}

/// <summary>Причуда выдана: системы эффектов делают одноразовую настройку (add_unique, post_add).</summary>
[ByRefEvent]
public readonly record struct ImperialQuirkAddedEvent(ProtoId<TraitPrototype> Quirk, ICommonSession? Player);
