using System.Linq;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Стазисный кейс для рыбы (/obj/item/storage/fish_case): одна рыба или предмет для аквариума,
/// размер кейса подстраивается под содержимое, рыба внутри в стазисе.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialFishCaseComponent : Component
{
    /// <summary>adjust_size: размер кейса растёт до размера содержимого.</summary>
    [DataField]
    public bool AdjustSize = true;

    [DataField]
    public ProtoId<ItemSizePrototype> BaseSize = "Small";

    /// <summary>random_fish_type(): случайная рыба по random_case_rarity, при заданной среде — совместимая с ней.</summary>
    [DataField]
    public bool RandomFish;

    [DataField]
    public ImperialFishFluid? RandomFluid;

    /// <summary>Взвешенный список рыб для заполнения (импортный, чёрный рынок).</summary>
    [DataField]
    public Dictionary<EntProtoId, float> Table = new();

    /// <summary>Источник ловли, из таблицы которого берутся рыбы (зловещий кейс Синдиката).</summary>
    [DataField]
    public ProtoId<ImperialFishSourcePrototype>? SourceTable;

    /// <summary>Рыба с чёрного рынка уже мертва.</summary>
    [DataField]
    public bool Dead;

    [ViewVariables]
    public bool Filled;
}

public sealed class ImperialFishCaseSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly ImperialFishSystem _fish = default!;

    /// <summary>Кэш весов random_fish_type() по среде.</summary>
    private readonly Dictionary<string, Dictionary<string, float>> _randomTables = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialFishCaseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ImperialFishCaseComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<ImperialFishCaseComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ImperialFishCaseComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ImperialFishCaseComponent, ImperialFishStasisCheckEvent>(OnStasisCheck);
        SubscribeLocalEvent<ImperialFishCaseComponent, ImperialFishEnvironmentCheckEvent>(OnEnvironmentCheck);
        SubscribeLocalEvent<ImperialFishCaseComponent, ImperialFishGrowthCheckEvent>(OnGrowthCheck);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _randomTables.Clear());
    }

    private void OnMapInit(Entity<ImperialFishCaseComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Filled)
            return;

        ent.Comp.Filled = true;
        var type = GetFishType(ent.Comp);
        if (type == null || !_container.TryGetContainer(ent, StorageComponent.ContainerId, out var container))
            return;

        var fish = Spawn(type, Transform(ent).Coordinates);
        if (TryComp<ImperialFishComponent>(fish, out var comp))
        {
            // TRAIT_NO_FISHING_ACHIEVEMENT, TRAIT_FISH_LOW_PRICE.
            comp.LowPrice = true;
            if (ent.Comp.Dead)
                _fish.SetDead((fish, comp), true, silent: true);
        }

        _container.Insert(fish, container);
    }

    private string? GetFishType(ImperialFishCaseComponent comp)
    {
        if (comp.Table.Count > 0)
            return PickWeighted(comp.Table.ToDictionary(p => p.Key.Id, p => p.Value));

        if (comp.SourceTable is { } sourceId && _proto.TryIndex(sourceId, out var source))
        {
            var fishes = source.Table.Keys.Where(k => _proto.TryIndex<EntityPrototype>(k, out var p) && p.Components.ContainsKey("ImperialFish")).ToList();
            return fishes.Count > 0 ? _random.Pick(fishes) : null;
        }

        return comp.RandomFish ? RandomFishType(comp.RandomFluid) : null;
    }

    /// <summary>random_fish_type().</summary>
    public string? RandomFishType(ImperialFishFluid? fluid)
    {
        var key = fluid?.ToString() ?? "any";
        if (!_randomTables.TryGetValue(key, out var table))
        {
            table = new Dictionary<string, float>();
            foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || !proto.TryGetComponent<ImperialFishComponent>(out var fish, EntityManager.ComponentFactory))
                    continue;
                if (fish.RandomCaseRarity <= 0)
                    continue;
                if (fluid is { } required && !ImperialAquariumSystem.CompatibleFluids(fish.RequiredFluid).Contains(required))
                    continue;

                table[proto.ID] = fish.RandomCaseRarity;
            }

            _randomTables[key] = table;
        }

        return PickWeighted(table);
    }

    private string? PickWeighted(Dictionary<string, float> table)
    {
        var total = table.Values.Sum();
        if (total <= 0)
            return null;

        var roll = _random.NextFloat() * total;
        foreach (var (id, weight) in table)
        {
            roll -= weight;
            if (roll <= 0)
                return id;
        }

        return table.Keys.Last();
    }

    /// <summary>max_slots = 1, только рыбы и предметы для аквариума (TRAIT_AQUARIUM_CONTENT).</summary>
    private void OnInsertAttempt(Entity<ImperialFishCaseComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != StorageComponent.ContainerId)
            return;

        if (args.Container.ContainedEntities.Count > 0
            || !HasComp<ImperialFishComponent>(args.EntityUid) && !HasComp<ImperialAquariumPropComponent>(args.EntityUid))
            args.Cancel();
    }

    private void OnInserted(Entity<ImperialFishCaseComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != StorageComponent.ContainerId || !ent.Comp.AdjustSize)
            return;

        if (!TryComp<ItemComponent>(args.Entity, out var item) || !TryComp<ItemComponent>(ent, out var own))
            return;

        if (_item.GetSizePrototype(item.Size) > _item.GetSizePrototype(own.Size))
            _item.SetSize(ent, item.Size, own);
    }

    private void OnRemoved(Entity<ImperialFishCaseComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != StorageComponent.ContainerId || !ent.Comp.AdjustSize)
            return;

        _item.SetSize(ent, ent.Comp.BaseSize);
    }

    /// <summary>fish_safe_storage: TRAIT_FISH_STASIS.</summary>
    private void OnStasisCheck(Entity<ImperialFishCaseComponent> ent, ref ImperialFishStasisCheckEvent args)
    {
        args.Stasis = true;
    }

    private void OnEnvironmentCheck(Entity<ImperialFishCaseComponent> ent, ref ImperialFishEnvironmentCheckEvent args)
    {
        args.Handled = true;
        args.Proper = true;
    }

    private void OnGrowthCheck(Entity<ImperialFishCaseComponent> ent, ref ImperialFishGrowthCheckEvent args)
    {
        args.Stopped = true;
    }
}
