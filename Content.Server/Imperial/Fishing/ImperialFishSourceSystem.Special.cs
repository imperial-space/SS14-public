using Robust.Shared.Random;
using System.Linq;
using Content.Server.Botany.Components;
using Content.Server.Cargo.Systems;
using Content.Shared.Chasm;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.VendingMachines;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Особые источники рыбы: случайные семена, «мусор» пропасти, торговые автоматы, гидропоника.
/// </summary>
public sealed partial class ImperialFishSourceSystem
{
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ImperialFishBreedingSystem _breeding = default!;

    /// <summary>all_portal_fish_sources_at_once: таблица портала-рандомайзера, создаётся один раз.</summary>
    private Dictionary<string, float>? _randomPortalTable;

    /// <summary>PAYCHECK_LOWER.</summary>
    private const int PaycheckLower = 25;

    /// <summary>FISHING_PRODUCT_DIFFICULTY_MULT.</summary>
    private const float ProductDifficultyMult = 1.6f;

    /// <summary>Вещи, упавшие в пропасть (GLOB.chasm_storage): сущность и место падения.</summary>
    private readonly List<(EntityUid Entity, MapCoordinates Where)> _chasmStorage = new();

    private EntityUid? _chasmStorageEntity;

    private List<string>? _seeds;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VendingMachineComponent, ImperialFishingRollRewardEvent>(OnVendingRoll);
        SubscribeLocalEvent<VendingMachineComponent, ImperialFishingCompletedEvent>(OnVendingCompleted);
    }

    #region Особые награды

    private bool TrySpecialReward(ImperialFishSourcePrototype source, string reward, EntityCoordinates coords, out EntityUid? spawned)
    {
        spawned = null;
        switch (reward)
        {
            case "randomSeed":
                spawned = Spawn(_random.Pick(GetSeeds()), coords);
                return true;
            case "chasmDetritus":
                spawned = DispenseDetritus("Any", coords);
                return true;
            case "randomOrgan":
            case "randomArm":
            case "influence":
            case "vendingChuck":
                return true;
        }

        return false;
    }

    /// <summary>Семена без экзотики (TG: yield != -1 и rarity &lt; PLANT_MODERATELY_RARE).</summary>
    private List<string> GetSeeds()
    {
        if (_seeds != null)
            return _seeds;

        _seeds = _proto.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && p.ID != "SeedBase" && _proto.EnumerateParents<EntityPrototype>(p.ID).Any(parent => parent.ID == "SeedBase"))
            .Where(p => !p.Categories.Any(c => c.ID == "HideSpawnMenu"))
            .Select(p => p.ID)
            .ToList();

        if (_seeds.Count == 0)
            _seeds.Add("WheatSeeds");

        return _seeds;
    }

    #endregion

    #region Пропасть

    /// <summary>Предметы, падающие в пропасть, не исчезают, а остаются на дне (chasm_storage).</summary>
    private void UpdateChasmStorage()
    {
        var query = EntityQueryEnumerator<ChasmFallingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var falling, out var xform))
        {
            if (_timing.CurTime < falling.NextDeletionTime - TimeSpan.FromSeconds(0.15))
                continue;

            if (!HasComp<ItemComponent>(uid) || HasComp<MobStateComponent>(uid))
                continue;

            var where = _transform.GetMapCoordinates(uid, xform);
            RemComp<ChasmFallingComponent>(uid);
            _chasmStorageEntity ??= Spawn(null, MapCoordinates.Nullspace);
            var container = _container.EnsureContainer<Container>(_chasmStorageEntity.Value, "imperial-chasm-storage");
            if (_container.Insert(uid, container, force: true))
                _chasmStorage.Add((uid, where));
        }

        _chasmStorage.RemoveAll(s => TerminatingOrDeleted(s.Entity));
    }

    /// <summary>/datum/chasm_detritus/dispense_detritus().</summary>
    public EntityUid? DispenseDetritus(string kind, EntityCoordinates coords)
    {
        var defaultChance = kind == "Any" ? 0.25f : 0.125f;
        var defaults = kind switch
        {
            "Bodies" => new Dictionary<string, float> { ["Remains"] = 6 },
            "Objects" => new Dictionary<string, float> { ["MaterialBones1"] = 28 },
            _ => new Dictionary<string, float> { ["MaterialBones1"] = 6, ["Remains"] = 2 },
        };

        if (!_random.Prob(defaultChance) && kind != "Bodies")
        {
            var origin = _transform.ToMapCoordinates(coords);
            var nearby = _chasmStorage
                .Where(s => !TerminatingOrDeleted(s.Entity) && s.Where.MapId == origin.MapId && (s.Where.Position - origin.Position).Length() <= 8)
                .ToList();

            if (nearby.Count > 0)
            {
                var picked = _random.Pick(nearby);
                _chasmStorage.Remove(picked);
                _container.TryRemoveFromContainer(picked.Entity, true);
                _transform.SetCoordinates(picked.Entity, coords);
                return picked.Entity;
            }
        }

        return Spawn(PickWeighted(defaults), coords);
    }

    #endregion

    #region Торговые автоматы

    /// <summary>get_vending_table(): чем ближе цена товара к стоимости наживки, тем вероятнее.</summary>
    private void OnVendingRoll(Entity<VendingMachineComponent> ent, ref ImperialFishingRollRewardEvent args)
    {
        var baitValue = args.Rod.Bait is { } bait ? Math.Max(_bait.GetCreditValue(bait), 1) : 1;
        var table = new Dictionary<string, float>();
        var entries = ent.Comp.Inventory.Values.Concat(ent.Comp.ContrabandInventory.Values).ToList();
        var priced = entries.Select(e => (Entry: e, Price: _proto.TryIndex<EntityPrototype>(e.ID, out var p) ? _pricing.GetEstimatedPrice(p) : 0))
            .OrderByDescending(e => e.Price)
            .ToList();

        double highest = 0;
        foreach (var (entry, price) in priced)
        {
            if (entry.Amount <= 0)
            {
                table[ImperialFishing.Dud] = table.GetValueOrDefault(ImperialFishing.Dud) + PaycheckLower;
                continue;
            }

            if (highest == 0)
                highest = Math.Max(price, 1);

            var high = Math.Max(highest, baitValue);
            var low = Math.Min(highest, baitValue);
            table[$"vending:{entry.ID}"] = (float) (low / high * 1000);
        }

        // add_risks()
        var available = priced.Count(p => p.Entry.Amount > 0);
        if (baitValue > highest)
        {
            table.Remove(ImperialFishing.Dud);
        }
        else
        {
            var malus = Math.Min(PaycheckLower - baitValue, highest);
            if (malus > 0)
            {
                table[ImperialFishing.Dud] = table.GetValueOrDefault(ImperialFishing.Dud) + (float) malus * available * 0.5f;
                table["vendingChuck"] = table.GetValueOrDefault("vendingChuck") + (float) malus * available * 0.5f;
            }
        }

        args.RewardProto = PickWeighted(table) ?? ImperialFishing.Dud;
    }

    /// <summary>Добавка к сложности у автоматов (get_product_difficulty).</summary>
    public float GetVendingDifficulty(string? reward, ImperialFishingRodInfo rod)
    {
        var baitValue = rod.Bait is { } bait ? _bait.GetCreditValue(bait) : 0;
        float diff = PaycheckLower - baitValue;
        if (reward != null && reward.StartsWith("vending:") && _proto.TryIndex<EntityPrototype>(reward.Substring(8), out var proto))
            diff = MathF.Min(diff, (float) _pricing.GetEstimatedPrice(proto));

        return diff * ProductDifficultyMult;
    }

    /// <summary>dispense_reward(): выдать товар или опрокинуть автомат на рыбака.</summary>
    private void OnVendingCompleted(Entity<VendingMachineComponent> ent, ref ImperialFishingCompletedEvent args)
    {
        if (!args.Win)
            return;

        var reward = args.Challenge.RewardProto;
        var user = args.Challenge.User;
        if (reward == "vendingChuck")
        {
            args.Handled = true;
            // tilt(): автомат падает на рыбака.
            var damage = new DamageSpecifier(_proto.Index<Content.Shared.Damage.Prototypes.DamageTypePrototype>(new Robust.Shared.Prototypes.ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype>("Blunt")), FixedPoint2.New(40));
            _damageable.TryChangeDamage(user, damage, origin: ent);
            _stun.TryUpdateParalyzeDuration(user, TimeSpan.FromSeconds(4));
            _popup.PopupEntity(Loc.GetString("imperial-fishing-vending-chuck", ("machine", ent.Owner)), user, Content.Shared.Popups.PopupType.LargeCaution);
            return;
        }

        if (reward == null || !reward.StartsWith("vending:"))
            return;

        args.Handled = true;
        var id = reward.Substring(8);
        var entry = ent.Comp.Inventory.GetValueOrDefault(id) ?? ent.Comp.ContrabandInventory.GetValueOrDefault(id);
        if (entry == null || entry.Amount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-dud-nothing"), user, user);
            return;
        }

        entry.Amount--;
        Dirty(ent);
        var item = Spawn(id, Transform(user).Coordinates);
        _hands.PickupOrDrop(user, item);
        _popup.PopupEntity(Loc.GetString("imperial-fishing-caught", ("reward", item)), user, user);

        // Вы платите за то, что получили.
        var bait = EntityManager.System<Content.Shared.Containers.ItemSlots.ItemSlotsSystem>()
            .GetItemOrNull(args.Challenge.Rod, ImperialFishingRodComponent.BaitSlot);
        if (bait != null && _bait.GetCreditValue(bait.Value) > 0)
            QueueDel(bait.Value);
    }

    #endregion

    /// <summary>Гидропоника: ловить можно только в лотке с водой и без растения.</summary>
    public string? SpecialReasonCantFish(ImperialFishSourcePrototype source, EntityUid location)
    {
        if (source.Special == "hydroTray" && TryComp<PlantHolderComponent>(location, out var holder))
        {
            if (holder.WaterLevel <= 0)
                return "imperial-fishing-hydro-no-water";
            if (holder.Seed != null)
                return "imperial-fishing-hydro-plant";
        }

        return null;
    }

    /// <summary>/datum/fish_source/portal/random/on_fishing_spot_init(): все награды других порталов с весом 1–4 и клетчатая голорыба.</summary>
    public Dictionary<string, float> GetRandomPortalTable()
    {
        if (_randomPortalTable != null)
            return _randomPortalTable;

        var table = new Dictionary<string, float> { ["ImperialFishHoloCheckered"] = 1 };
        foreach (var portal in _proto.EnumeratePrototypes<ImperialFishSourcePrototype>())
        {
            if (!portal.ID.StartsWith("ImperialFishSourcePortal") || portal.Special == "portalRandom")
                continue;

            foreach (var reward in portal.Table.Keys)
            {
                if (reward != ImperialFishing.Dud)
                    table[reward] = _random.Next(1, 5);
            }
        }

        _randomPortalTable = table;
        return table;
    }

    /// <summary>spawn_reward() рандомайзера: 1–4 случайные черты (вес — наследуемость² / 100) и разброс размера 0.3.</summary>
    private void RandomizePortalFish(Entity<ImperialFishComponent> fish)
    {
        var weighted = new Dictionary<string, float>();
        foreach (var trait in _proto.EnumeratePrototypes<ImperialFishTraitPrototype>())
        {
            var weight = MathF.Round(trait.Inheritability * trait.Inheritability / 100f);
            if (weight > 0)
                weighted[trait.ID] = weight;
        }

        var traits = new List<string>();
        var count = _random.Next(1, 5);
        for (var i = 0; i < count; i++)
        {
            if (PickWeighted(weighted) is { } picked && !traits.Contains(picked))
                traits.Add(picked);
        }

        _breeding.InheritTraits(fish, traits, null, null, null);
        _fish.RandomizeSizeAndWeight(fish, deviation: 0.3f);
    }
}
