using System.Linq;
using System.Reflection;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Examine;
using Content.Shared.Imperial.MachineParts;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MachineParts;

/// <summary>Исходные значения улучшаемых полей машины (до учёта деталей).</summary>
[RegisterComponent]
public sealed partial class ImperialMachineUpgradeComponent : Component
{
    [ViewVariables]
    public Dictionary<string, object?> Base = new();
}

/// <summary>Детали машины изменились (RefreshParts). Поднимается на машине.</summary>
[ByRefEvent]
public readonly record struct ImperialMachinePartsRefreshEvent(ImperialMachineRatings Ratings);

/// <summary>Уровни деталей машины по типам.</summary>
public sealed class ImperialMachineRatings
{
    public readonly Dictionary<ImperialStockPartType, List<int>> Tiers = new();

    /// <summary>Универсальные детали (стак «Manipulator») — 1-й уровень любого типа.</summary>
    public int Generic;

    /// <summary>Средний уровень деталей этого типа; без таких деталей — 1.</summary>
    public float Avg(ImperialStockPartType type)
    {
        return Tiers.TryGetValue(type, out var list) && list.Count > 0 ? (float) list.Average() : 1f;
    }

    /// <summary>Сумма уровней деталей этого типа (как цикл по component_parts в TG); без таких деталей — fallback.</summary>
    public int Sum(ImperialStockPartType type, int fallback)
    {
        return Tiers.TryGetValue(type, out var list) && list.Count > 0 ? list.Sum() : fallback;
    }

    public int Max(ImperialStockPartType type)
    {
        return Tiers.TryGetValue(type, out var list) && list.Count > 0 ? list.Max() : 1;
    }
}

/// <summary>
/// Уровни деталей машин (stock_parts TG): детали с уровнем вставляются в рамку вместо универсальной детали,
/// рейтинг машины пересчитывается при каждой смене деталей и меняет её характеристики по формулам RefreshParts().
/// Поля машин SS14 закрыты атрибутами доступа их систем, поэтому меняются через отражение.
/// </summary>
public sealed class ImperialMachinePartsSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MachineFrameSystem _frame = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public const string GenericStack = "Manipulator";

    private readonly HashSet<EntityUid> _dirty = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialStockPartComponent, BeforeRangedInteractEvent>(OnPartInteract);
        SubscribeLocalEvent<ImperialStockPartComponent, ExaminedEvent>(OnPartExamined);
        SubscribeLocalEvent<MachineComponent, EntInsertedIntoContainerMessage>(OnMachineParts);
        SubscribeLocalEvent<MachineComponent, EntRemovedFromContainerMessage>(OnMachinePartsRemoved);
    }

    #region Детали

    private void OnPartExamined(Entity<ImperialStockPartComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-stock-part-examine", ("tier", ent.Comp.Tier)));
    }

    /// <summary>Деталь с уровнем засчитывается в рамке за универсальную деталь.</summary>
    private void OnPartInteract(Entity<ImperialStockPartComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !TryComp<MachineFrameComponent>(target, out var frame))
            return;

        // Требования по тегам и компонентам рамка обработает сама.
        if (FrameNeedsTag(frame, ent))
            return;

        if (TryInsertIntoFrame((target, frame), ent))
            args.Handled = true;
    }

    private bool FrameNeedsTag(MachineFrameComponent frame, EntityUid part)
    {
        foreach (var (tag, info) in frame.TagRequirements)
        {
            if (frame.TagProgress.GetValueOrDefault(tag) < info.Amount && _tag.HasTag(part, tag))
                return true;
        }

        return false;
    }

    /// <summary>Положить деталь в рамку в счёт универсальных деталей.</summary>
    public bool TryInsertIntoFrame(Entity<MachineFrameComponent> frame, EntityUid part)
    {
        var comp = frame.Comp;
        if (!comp.HasBoard || !comp.MaterialRequirements.TryGetValue(GenericStack, out var required))
            return false;

        var progress = comp.MaterialProgress.GetValueOrDefault(GenericStack);
        if (progress >= required)
            return false;

        _container.TryRemoveFromContainer(part);
        if (!_container.Insert(part, comp.PartContainer))
            return false;

        comp.MaterialProgress[GenericStack] = progress + 1;
        if (_frame.IsComplete(comp))
            _popup.PopupEntity(Loc.GetString("machine-frame-component-on-complete"), frame);

        return true;
    }

    #endregion

    #region Профили (req_components плат TG)

    private static readonly (string Component, ImperialStockPartType[] Parts)[] Profiles =
    {
        ("Lathe", new[] { ImperialStockPartType.Servo, ImperialStockPartType.MatterBin, ImperialStockPartType.Servo, ImperialStockPartType.MatterBin }),
        ("Microwave", new[] { ImperialStockPartType.MicroLaser, ImperialStockPartType.MatterBin, ImperialStockPartType.Capacitor }),
        ("ReagentGrinder", new[] { ImperialStockPartType.Servo, ImperialStockPartType.MatterBin }),
        ("GasThermoMachine", new[] { ImperialStockPartType.MatterBin, ImperialStockPartType.MatterBin, ImperialStockPartType.MicroLaser }),
        ("Emitter", new[] { ImperialStockPartType.MicroLaser, ImperialStockPartType.Servo }),
        ("Charger", new[] { ImperialStockPartType.Capacitor }),
        ("SeedExtractor", new[] { ImperialStockPartType.Servo, ImperialStockPartType.MatterBin }),
        ("FatExtractor", new[] { ImperialStockPartType.MicroLaser }),
        ("BiomassReclaimer", new[] { ImperialStockPartType.Servo, ImperialStockPartType.MatterBin }),
        ("MedicalScanner", new[] { ImperialStockPartType.ScanningModule }),
        ("CloningPod", new[] { ImperialStockPartType.Servo, ImperialStockPartType.ScanningModule }),
        ("SpaceHeater", new[] { ImperialStockPartType.MicroLaser, ImperialStockPartType.Capacitor }),
        ("MaterialReclaimer", new[] { ImperialStockPartType.Servo, ImperialStockPartType.MatterBin }),
        ("AnomalyVessel", new[] { ImperialStockPartType.Capacitor }),
        ("StasisBed", new[] { ImperialStockPartType.Capacitor }),
        ("FuelGenerator", new[] { ImperialStockPartType.Capacitor }),
        ("ImperialFishingPortal", new[] { ImperialStockPartType.MatterBin, ImperialStockPartType.MatterBin, ImperialStockPartType.Capacitor }),
    };

    /// <summary>Какие детали нужны машине (по составу платы в TG); пусто — любые.</summary>
    public ImperialStockPartType[] GetProfile(EntityPrototype? machine)
    {
        if (machine == null)
            return Array.Empty<ImperialStockPartType>();

        foreach (var (component, parts) in Profiles)
        {
            if (machine.Components.ContainsKey(component))
                return parts;
        }

        return Array.Empty<ImperialStockPartType>();
    }

    #endregion

    #region Рейтинг

    public ImperialMachineRatings GetRatings(EntityUid machine)
    {
        var ratings = new ImperialMachineRatings();
        if (!_container.TryGetContainer(machine, MachineFrameComponent.PartContainerName, out var container))
            return ratings;

        foreach (var part in container.ContainedEntities)
        {
            if (TryComp<ImperialStockPartComponent>(part, out var stock))
            {
                if (!ratings.Tiers.TryGetValue(stock.Type, out var list))
                    ratings.Tiers[stock.Type] = list = new List<int>();
                list.Add(stock.Tier);
            }
            else if (TryComp<StackComponent>(part, out var stack) && stack.StackTypeId == GenericStack)
            {
                ratings.Generic += stack.Count;
            }
        }

        return ratings;
    }

    private void OnMachineParts(Entity<MachineComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == MachineFrameComponent.PartContainerName)
            _dirty.Add(ent);
    }

    private void OnMachinePartsRemoved(Entity<MachineComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == MachineFrameComponent.PartContainerName)
            _dirty.Add(ent);
    }

    public void Refresh(EntityUid machine)
    {
        _dirty.Add(machine);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_dirty.Count == 0)
            return;

        foreach (var uid in _dirty.ToList())
        {
            _dirty.Remove(uid);
            if (TerminatingOrDeleted(uid) || !HasComp<MachineComponent>(uid))
                continue;

            var ratings = GetRatings(uid);
            ApplyUpgrades(uid, ratings);
            var ev = new ImperialMachinePartsRefreshEvent(ratings);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    #endregion

    #region Бонусы (RefreshParts)

    private void ApplyUpgrades(EntityUid uid, ImperialMachineRatings r)
    {
        var bin = r.Avg(ImperialStockPartType.MatterBin);
        var servo = r.Avg(ImperialStockPartType.Servo);
        var laser = r.Avg(ImperialStockPartType.MicroLaser);
        var cap = r.Avg(ImperialStockPartType.Capacitor);
        var scan = r.Avg(ImperialStockPartType.ScanningModule);

        // Автолат и производственные машины РнД: расход материалов и время печати от сервоприводов, ёмкость — от контейнеров материи.
        if (Get(uid, "Lathe") is { } lathe)
        {
            var autolathe = Prototype(uid)?.ID.Contains("Autolathe") == true;
            float materials, time;
            if (autolathe)
            {
                // creation_efficiency: 1.6 → 1.4 → 1.2 → 1.0.
                materials = MathF.Max(1f, MathF.Round(1.8f - 0.2f * servo, 1)) / 1.6f;
                time = 1f;
            }
            else
            {
                // efficiency_coeff = 1.2 − 0.1 × сумма уровней двух сервоприводов; время печати ∝ coeff^0.8.
                var coeff = MathF.Max(0.1f, 1.2f - 0.1f * 2 * servo);
                materials = coeff;
                time = MathF.Pow(coeff, 0.8f);
            }

            Scale(uid, lathe, "MaterialUseMultiplier", materials);
            Scale(uid, lathe, "TimeMultiplier", time);
        }

        if (Get(uid, "MaterialStorage") is { } storage && Base(uid, storage, "StorageLimit") is int limit)
            Set(uid, storage, "StorageLimit", (int?) (int) MathF.Round(limit * bin));

        // Микроволновка: efficiency = сумма уровней микролазеров.
        if (Get(uid, "Microwave") is { } microwave)
            Scale(uid, microwave, "CookTimeMultiplier", 1f / laser);

        if (Get(uid, "ReagentGrinder") is { } grinder)
            Scale(uid, grinder, "WorkTimeMultiplier", MathF.Pow(0.75f, servo - 1));

        // Термомашины: heat_capacity = 5000 × (сумма уровней 2 контейнеров − 1)².
        if (Get(uid, "GasThermoMachine") is { } thermo)
            Scale(uid, thermo, "HeatCapacity", MathF.Pow(2 * bin - 1, 2));

        // Эмиттер: задержки 12 − 2 × уровень лазера (из 10 у 1-го уровня), мощность 350 − 50 × сервопривод.
        if (Get(uid, "Emitter") is { } emitter)
        {
            var delay = (12f - 2f * laser) / 10f;
            ScaleTime(uid, emitter, "FireBurstDelayMin", delay);
            ScaleTime(uid, emitter, "FireBurstDelayMax", delay);
            if (Base(uid, emitter, "PowerUseActive") is int power)
                Set(uid, emitter, "PowerUseActive", (int) MathF.Round(power * (350f - 50f * servo) / 300f));
        }

        // Зарядники: recharge_coeff = уровень конденсатора.
        if (Get(uid, "Charger") is { } charger)
            Scale(uid, charger, "ChargeRate", cap);

        // Экстрактор семян: seed_multiplier × уровень сервопривода.
        if (Get(uid, "SeedExtractor") is { } extractor)
        {
            ScaleInt(uid, extractor, "BaseMinSeeds", servo);
            ScaleInt(uid, extractor, "BaseMaxSeeds", servo);
        }

        // Липоотсос: bite_size + 2.5 × лазер, nutrient_to_meat − 5 × лазер.
        if (Get(uid, "FatExtractor") is { } fat)
        {
            if (Base(uid, fat, "NutritionPerSecond") is int bite)
                Set(uid, fat, "NutritionPerSecond", bite + (int) MathF.Round(2.5f * (laser - 1)));
            if (Base(uid, fat, "NutrientPerMeat") is int perMeat)
                Set(uid, fat, "NutrientPerMeat", Math.Max(5, perMeat - (int) MathF.Round(5 * (laser - 1))));
        }

        // Переработчик биомассы (гиббер): выход от контейнеров, скорость от сервоприводов.
        if (Get(uid, "BiomassReclaimer") is { } reclaimer)
        {
            Scale(uid, reclaimer, "YieldPerUnitMass", (1f + 0.25f * bin) / 1.25f);
            Scale(uid, reclaimer, "ProcessingTimePerUnitMass", (40f - 5f * servo) / 35f);
        }

        if (Get(uid, "MedicalScanner") is { } scanner)
            Scale(uid, scanner, "CloningFailChanceMultiplier", 1f / scan);

        if (Get(uid, "CloningPod") is { } pod)
            Scale(uid, pod, "CloningTime", 1f / servo);

        if (Get(uid, "SpaceHeater") is { } heater)
        {
            Scale(uid, heater, "HeatingCp", laser);
            Scale(uid, heater, "CoolingCp", laser);
        }

        if (Get(uid, "MaterialReclaimer") is { } recycler)
            Scale(uid, recycler, "MaterialProcessRate", servo);

        if (Get(uid, "AnomalyVessel") is { } vessel)
            Scale(uid, vessel, "PointMultiplier", MathF.Pow(1.5f, cap - 1));

        if (Get(uid, "StasisBed") is { } stasis)
            Scale(uid, stasis, "Multiplier", 1f + 0.25f * (cap - 1));

        if (Get(uid, "FuelGenerator") is { } generator)
            Scale(uid, generator, "MaxTargetPower", 1f + 0.5f * (cap - 1));
    }

    private IComponent? Get(EntityUid uid, string name)
    {
        return _factory.TryGetRegistration(name, out var registration) && EntityManager.TryGetComponent(uid, registration.Type, out var comp)
            ? comp
            : null;
    }

    private static MemberInfo? Member(object comp, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = comp.GetType();
        return (MemberInfo?) type.GetField(name, flags) ?? type.GetProperty(name, flags);
    }

    private static object? Read(object comp, string name)
    {
        return Member(comp, name) switch
        {
            FieldInfo field => field.GetValue(comp),
            PropertyInfo property => property.GetValue(comp),
            _ => null,
        };
    }

    /// <summary>Значение поля до учёта деталей (запоминается при первом обращении).</summary>
    private object? Base(EntityUid uid, IComponent comp, string name)
    {
        var upgrade = EnsureComp<ImperialMachineUpgradeComponent>(uid);
        var key = comp.GetType().Name + "." + name;
        if (!upgrade.Base.TryGetValue(key, out var value))
            upgrade.Base[key] = value = Read(comp, name);

        return value;
    }

    private void Set(EntityUid uid, IComponent comp, string name, object? value)
    {
        switch (Member(comp, name))
        {
            case FieldInfo field:
                field.SetValue(comp, value);
                break;
            case PropertyInfo { CanWrite: true } property:
                property.SetValue(comp, value);
                break;
            default:
                return;
        }

        if (comp is Component component && component.NetSyncEnabled)
            Dirty(uid, component);
    }

    private void Scale(EntityUid uid, IComponent comp, string name, float factor)
    {
        if (Base(uid, comp, name) is float value)
            Set(uid, comp, name, value * factor);
    }

    private void ScaleInt(EntityUid uid, IComponent comp, string name, float factor)
    {
        if (Base(uid, comp, name) is int value)
            Set(uid, comp, name, Math.Max(1, (int) MathF.Round(value * factor)));
    }

    private void ScaleTime(EntityUid uid, IComponent comp, string name, float factor)
    {
        if (Base(uid, comp, name) is TimeSpan value)
            Set(uid, comp, name, value * factor);
    }

    #endregion
}
