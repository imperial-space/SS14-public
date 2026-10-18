using System.Linq;
using Content.Server.Botany.Components;
using Content.Server.Cargo.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Cargo;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Imperial.Cargo;
using Content.Shared.Materials;
using Content.Shared.Nutrition.Components;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cargo;

/// <summary>
/// Экспорт TG (code/modules/cargo/exports.dm): у предмета, подходящего под /datum/export, цена считается по TG
/// и падает от продаж (k_elasticity), со временем восстанавливаясь. Остальные предметы оцениваются как раньше.
/// </summary>
public sealed class ImperialCargoExportSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly ImperialStockMarketSystem _market = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> OreTag = "Ore";

    /// <summary>MAX_GAS_CREDITS.</summary>
    private const float MaxGasCredits = 15000;

    /// <summary>SHEET_MATERIAL_AMOUNT.</summary>
    private const float SheetAmount = 100;

    /// <summary>base_value газов TG, которые оценивает /datum/export/gas_canister.</summary>
    private static readonly Dictionary<Gas, float> GasValues = new()
    {
        [Gas.BZ] = 1.5f,
        [Gas.HyperNoblium] = 2.5f,
        [Gas.Tritium] = 2.5f,
        [Gas.Frezon] = 5f,
        [Gas.Hydrogen] = 1f,
        [Gas.AntiNoblium] = 10f,
    };

    private List<ImperialCargoExportPrototype> _exports = new();

    /// <summary>Прототип сущности → экспорт по типу (кэш).</summary>
    private readonly Dictionary<string, ImperialCargoExportPrototype?> _byPrototype = new();

    /// <summary>k_elasticity по экспортам.</summary>
    private readonly Dictionary<string, float> _elasticity = new();

    /// <summary>discovered_plants: вид семян → проданная потенция.</summary>
    private readonly Dictionary<string, float> _discoveredPlants = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MetaDataComponent, PriceCalculationEvent>(OnPrice);
        SubscribeLocalEvent<EntitySoldEvent>(OnSold);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnReload);

        Rebuild();
    }

    private void OnReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ImperialCargoExportPrototype>() || args.WasModified<EntityPrototype>())
            Rebuild();
    }

    private void Rebuild()
    {
        _exports = _proto.EnumeratePrototypes<ImperialCargoExportPrototype>().OrderBy(e => e.Priority).ToList();
        _byPrototype.Clear();
    }

    #region Подбор экспорта

    /// <summary>applies_to(): первый подходящий экспорт.</summary>
    public ImperialCargoExportPrototype? FindExport(EntityUid uid)
    {
        var proto = MetaData(uid).EntityPrototype;
        var byType = proto == null ? null : TypeExport(proto);

        foreach (var export in _exports)
        {
            if (byType != null && export.Priority >= byType.Priority)
                break;

            var applies = export.Kind switch
            {
                ImperialCargoExportKind.Material => SingleMaterial(uid, export) > 0,
                ImperialCargoExportKind.Food => TryComp<EdibleComponent>(uid, out var edible) && edible.Solution == "food"
                                                && !HasComp<ProduceComponent>(uid) && !IsExcluded(proto, export),
                ImperialCargoExportKind.Seed => HasComp<SeedComponent>(uid),
                ImperialCargoExportKind.GasCanister => HasComp<GasCanisterComponent>(uid),
                _ => false,
            };

            if (applies)
                return export;
        }

        return byType;
    }

    private static bool IsTypeBased(ImperialCargoExportPrototype export)
    {
        return export.Kind is ImperialCargoExportKind.Item or ImperialCargoExportKind.Stack or ImperialCargoExportKind.ReagentTank;
    }

    /// <summary>Первый экспорт по типу прототипа (кэшируется).</summary>
    private ImperialCargoExportPrototype? TypeExport(EntityPrototype proto)
    {
        if (_byPrototype.TryGetValue(proto.ID, out var cached))
            return cached;

        var found = _exports.FirstOrDefault(e => IsTypeBased(e) && TypeMatches(proto, e));
        _byPrototype[proto.ID] = found;
        return found;
    }

    private bool TypeMatches(EntityPrototype proto, ImperialCargoExportPrototype export)
    {
        if (IsExcluded(proto, export))
            return false;

        if (export.Entities.Any(e => e.Id == proto.ID))
            return true;

        return export.IncludeChildren
               && _proto.EnumerateParents<EntityPrototype>(proto.ID).Any(p => export.Entities.Any(e => e.Id == p.ID));
    }

    private bool IsExcluded(EntityPrototype? proto, ImperialCargoExportPrototype export)
    {
        if (proto == null || export.Exclude.Count == 0)
            return false;

        return export.Exclude.Any(e => e.Id == proto.ID)
               || _proto.EnumerateParents<EntityPrototype>(proto.ID).Any(p => export.Exclude.Any(e => e.Id == p.ID));
    }

    /// <summary>Листов материала экспорта в стопке из одного материала (руда — 80%).</summary>
    private int SingleMaterial(EntityUid uid, ImperialCargoExportPrototype export)
    {
        if (!TryComp<StackComponent>(uid, out var stack) || !TryComp<PhysicalCompositionComponent>(uid, out var composition))
            return 0;
        if (composition.MaterialComposition.Count != 1)
            return 0;

        var (material, perUnit) = composition.MaterialComposition.First();
        if (!export.Materials.Any(m => m.Id == material))
            return 0;

        float amount = perUnit * stack.Count;
        if (_tag.HasTag(uid, OreTag))
            amount *= 0.8f;

        return (int) MathF.Round(amount / SheetAmount);
    }

    #endregion

    #region Цена

    public float Elasticity(ImperialCargoExportPrototype export)
    {
        return _elasticity.TryGetValue(export.ID, out var k) ? k : export.Elasticity;
    }

    /// <summary>get_amount().</summary>
    private float Amount(EntityUid uid, ImperialCargoExportPrototype export)
    {
        return export.Kind switch
        {
            ImperialCargoExportKind.Stack => TryComp<StackComponent>(uid, out var stack) ? stack.Count : 1,
            ImperialCargoExportKind.Material => SingleMaterial(uid, export),
            _ => 1,
        };
    }

    /// <summary>get_base_cost().</summary>
    private float BaseCost(EntityUid uid, ImperialCargoExportPrototype export)
    {
        switch (export.Kind)
        {
            case ImperialCargoExportKind.Material when export.Market is { } market:
                return _market.Get(market)?.Price ?? export.Cost;
            case ImperialCargoExportKind.Seed:
                return SeedCost(uid, export);
            case ImperialCargoExportKind.GasCanister:
            {
                if (!TryComp<GasCanisterComponent>(uid, out var canister) || canister.Air.TotalMoles <= 0)
                    return 0;

                var worth = export.Cost;
                foreach (var (gas, value) in GasValues)
                {
                    var moles = canister.Air.GetMoles(gas);
                    if (moles <= 0)
                        continue;
                    worth += MathF.Ceiling(value * moles);
                    if (worth > MaxGasCredits)
                        return MaxGasCredits;
                }

                return worth;
            }
            case ImperialCargoExportKind.ReagentTank:
            {
                var fraction = 0f;
                if (_solution.TryGetSolution(uid, "tank", out _, out var tank) && tank.MaxVolume > 0)
                    fraction = (float) (tank.Volume / tank.MaxVolume);
                return export.Cost + MathF.Round(export.ExtraCost * fraction);
            }
            default:
                return export.Cost;
        }
    }

    /// <summary>seed/get_base_cost(): новый вид — образец, известный — только прирост потенции.</summary>
    private float SeedCost(EntityUid uid, ImperialCargoExportPrototype export)
    {
        if (!TryComp<SeedComponent>(uid, out var seed) || SeedKey(seed) is not { } key)
            return 0;

        var potency = seed.Seed?.Potency ?? 0;
        if (!_discoveredPlants.TryGetValue(key, out var discovered))
            return export.Cost;

        return Math.Max(0, export.ExtraCost * (potency - discovered));
    }

    private static string? SeedKey(SeedComponent seed) => seed.SeedId ?? seed.Seed?.Name;

    /// <summary>get_cost(): base × amount × k_elasticity, округление вверх.</summary>
    public float GetCost(EntityUid uid, ImperialCargoExportPrototype export)
    {
        var total = BaseCost(uid, export) * Amount(uid, export);
        if (export.Elasticity > 0)
            total *= Elasticity(export);

        return MathF.Ceiling(total);
    }

    private void OnPrice(EntityUid uid, MetaDataComponent meta, ref PriceCalculationEvent args)
    {
        if (args.Handled || FindExport(uid) is not { } export)
            return;

        var price = (double) GetCost(uid, export);

        // Содержимое продаётся отдельно (export_item_and_contents), поэтому считаем его здесь же.
        if (TryComp<ContainerManagerComponent>(uid, out var containers))
        {
            foreach (var container in containers.Containers.Values)
            {
                foreach (var ent in container.ContainedEntities)
                {
                    price += _pricing.GetPrice(ent);
                }
            }
        }

        args.Price = price;
        args.Handled = true;
    }

    #endregion

    #region Продажа

    /// <summary>sell_object(): эластичность, рынок и открытые виды семян.</summary>
    private void OnSold(ref EntitySoldEvent args)
    {
        foreach (var uid in args.Sold)
        {
            SellRecursive(uid);
        }
    }

    private void SellRecursive(EntityUid uid)
    {
        if (TryComp<ContainerManagerComponent>(uid, out var containers))
        {
            foreach (var container in containers.Containers.Values)
            {
                foreach (var ent in container.ContainedEntities)
                {
                    SellRecursive(ent);
                }
            }
        }

        if (FindExport(uid) is not { } export)
            return;

        var amount = Amount(uid, export);
        if (amount <= 0 || GetCost(uid, export) <= 0)
            return;

        if (export.Elasticity > 0)
            _elasticity[export.ID] = Math.Max(0, Elasticity(export) - amount * export.Hit);

        if (export.Kind == ImperialCargoExportKind.Seed && TryComp<SeedComponent>(uid, out var seed) && SeedKey(seed) is { } key)
            _discoveredPlants[key] = seed.Seed?.Potency ?? 0;

        if (export.Market is { } market && _market.Get(market) is { } state)
        {
            _market.AdjustPrice(market, -state.Price * (amount / (amount + state.Quantity)));
            _market.AdjustQuantity(market, (int) amount);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // process(): k_elasticity возвращается к 1 за k_recovery_time.
        foreach (var id in _elasticity.Keys.ToList())
        {
            if (!_proto.TryIndex<ImperialCargoExportPrototype>(id, out var export) || export.Recovery <= 0)
            {
                _elasticity.Remove(id);
                continue;
            }

            var k = _elasticity[id] + frameTime / export.Recovery;
            if (k >= 1)
                _elasticity.Remove(id);
            else
                _elasticity[id] = k;
        }
    }

    #endregion
}
