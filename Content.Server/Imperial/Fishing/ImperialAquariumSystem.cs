using System.Linq;
using Content.Server.Lightning;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Fluids;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Аквариумы (/datum/component/aquarium, /obj/structure/aquarium, /obj/item/fish_tank): среда и температура,
/// автоматический подбор настроек, автокормление, вставка рыб и декора, поломка стекла, интерфейс,
/// ловля рыбы из аквариума и биоэлектрический генератор.
/// </summary>
public sealed class ImperialAquariumSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly ImperialFishSystem _fish = default!;
    [Dependency] private readonly ImperialFishTraitSystem _traits = default!;
    [Dependency] private readonly ImperialFishBreedingSystem _breeding = default!;
    [Dependency] private readonly ImperialFishDataSystem _data = default!;
    [Dependency] private readonly ImperialFishingSlowdownSystem _slowdown = default!;

    /// <summary>integrity_failure 0.3 от прочности 300: стекло трескается при 210 урона.</summary>
    private const float BreakDamage = 210f;

    private static readonly ImperialFishFluid[] FluidTypes =
    {
        ImperialFishFluid.Saltwater,
        ImperialFishFluid.Freshwater,
        ImperialFishFluid.Sulphwater,
        ImperialFishFluid.Air,
    };

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialAquariumComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ImperialAquariumComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ImperialAquariumComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ImperialAquariumComponent, ImperialFishInContainerStatusChangedEvent>(OnFishStatusChanged);
        SubscribeLocalEvent<ImperialAquariumComponent, ImperialFishEnvironmentCheckEvent>(OnEnvironmentCheck);
        SubscribeLocalEvent<ImperialAquariumComponent, ImperialFishGrowthCheckEvent>(OnGrowthCheck);
        SubscribeLocalEvent<ImperialAquariumComponent, ImperialFishElectrogenesisEvent>(OnElectrogenesis);
        SubscribeLocalEvent<ImperialAquariumComponent, ImperialFishingRollRewardEvent>(OnRollReward);

        SubscribeLocalEvent<ImperialAquariumComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ImperialAquariumComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ImperialAquariumComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ImperialAquariumComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerbs);
        SubscribeLocalEvent<ImperialAquariumComponent, GetVerbsEvent<Verb>>(OnVerbs);
        SubscribeLocalEvent<ImperialAquariumComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialAquariumComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<ImperialAquariumComponent, ImperialAquariumAdmireDoAfterEvent>(OnAdmireDoAfter);
        SubscribeLocalEvent<ImperialAquariumComponent, ImperialAquariumRepairDoAfterEvent>(OnRepairDoAfter);

        SubscribeLocalEvent<ImperialAquariumUpgradeComponent, AfterInteractEvent>(OnUpgradeInteract);
        SubscribeLocalEvent<ImperialAquariumUpgradeComponent, ImperialAquariumUpgradeDoAfterEvent>(OnUpgradeDoAfter);

        Subs.BuiEvents<ImperialAquariumComponent>(ImperialAquariumUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>((uid, comp, _) => UpdateUi((uid, comp)));
            subs.Event<ImperialAquariumSetTemperatureMessage>(OnSetTemperature);
            subs.Event<ImperialAquariumSetFluidMessage>(OnSetFluid);
            subs.Event<ImperialAquariumSetModeMessage>(OnSetMode);
            subs.Event<ImperialAquariumSetFeedingIntervalMessage>(OnSetFeedingInterval);
            subs.Event<ImperialAquariumPetFishMessage>(OnPetFish);
            subs.Event<ImperialAquariumRemoveItemMessage>(OnRemoveItem);
            subs.Event<ImperialAquariumRenameFishMessage>(OnRenameFish);
        });
    }

    #region Основное

    private void OnMapInit(Entity<ImperialAquariumComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<Container>(ent, ImperialAquariumComponent.ContainerId);
        ent.Comp.Temperature = Math.Clamp(ent.Comp.Temperature, ent.Comp.MinTemperature, ent.Comp.MaxTemperature);
        ent.Comp.LastFeeding = _timing.CurTime;
        var mode = ent.Comp.Mode;
        ent.Comp.Mode = (ImperialAquariumMode) 255;
        SetMode(ent, mode);
        UpdateAppearance(ent);
        StartFill(ent);
    }

    private void StartFill(Entity<ImperialAquariumComponent> ent)
    {
        if (!TryComp<ImperialAquariumStartFillComponent>(ent, out var fill) || fill.Variants.Count == 0)
            return;

        var roll = _random.NextFloat() * fill.Variants.Sum(v => v.Weight);
        var variant = fill.Variants[^1];
        foreach (var entry in fill.Variants)
        {
            roll -= entry.Weight;
            if (roll > 0)
                continue;

            variant = entry;
            break;
        }

        var container = _container.EnsureContainer<Container>(ent, ImperialAquariumComponent.ContainerId);
        foreach (var proto in variant.Contents)
            _container.Insert(Spawn(proto, Transform(ent).Coordinates), container);

        if (variant.Reagent != null && _solution.TryGetSolution(ent.Owner, ImperialAquariumComponent.FeedSolution, out var feed, out _))
            _solution.TryAddReagent(feed.Value, variant.Reagent, variant.Amount);

        RemComp<ImperialAquariumStartFillComponent>(ent);
    }

    public List<Entity<ImperialFishComponent>> GetFishes(EntityUid aquarium) => _breeding.GetAquariumFishes(aquarium);

    public static IEnumerable<ImperialFishFluid> CompatibleFluids(ImperialFishFluid required)
    {
        return required switch
        {
            ImperialFishFluid.AnyWater => new[] { ImperialFishFluid.Saltwater, ImperialFishFluid.Freshwater, ImperialFishFluid.Sulphwater },
            ImperialFishFluid.Anadromous => new[] { ImperialFishFluid.Saltwater, ImperialFishFluid.Freshwater },
            _ => new[] { required },
        };
    }

    /// <summary>check_fluid_and_temperature().</summary>
    private void CheckFluidAndTemperature(ImperialAquariumComponent aquarium, ImperialFishComponent fish)
    {
        fish.SafeFluid = CompatibleFluids(fish.RequiredFluid).Contains(aquarium.Fluid)
                         || aquarium.Fluid == ImperialFishFluid.Air && _traits.Has(fish, "Amphibious");
        fish.SafeTemperature = aquarium.Temperature >= fish.TemperatureMin && aquarium.Temperature <= fish.TemperatureMax;
    }

    private void CheckAllFish(Entity<ImperialAquariumComponent> ent)
    {
        foreach (var fish in GetFishes(ent))
            CheckFluidAndTemperature(ent.Comp, fish.Comp);
    }

    private void OnInserted(Entity<ImperialAquariumComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ImperialAquariumComponent.ContainerId || !TryComp<ImperialFishComponent>(args.Entity, out var fish))
            return;

        UpdateOverpopulation(ent);
        if (ent.Comp.Mode == ImperialAquariumMode.Auto && !fish.Dead)
            GetOptimalSettings(ent);
        else
            CheckFluidAndTemperature(ent.Comp, fish);

        UpdatePortable(ent);
        UpdateUi(ent);
    }

    private void OnRemoved(Entity<ImperialAquariumComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ImperialAquariumComponent.ContainerId || !TryComp<ImperialFishComponent>(args.Entity, out var fish))
            return;

        fish.Overpopulated = false;
        fish.SafeFluid = false;
        fish.SafeTemperature = false;
        UpdateOverpopulation(ent);
        if (ent.Comp.Mode == ImperialAquariumMode.Auto && !fish.Dead)
            GetOptimalSettings(ent);

        UpdatePortable(ent);
        UpdateUi(ent);
    }

    /// <summary>FISH_FLAG_OVERPOPULATED: рыб одного вида больше стабильной популяции.</summary>
    private void UpdateOverpopulation(Entity<ImperialAquariumComponent> ent)
    {
        foreach (var group in GetFishes(ent).GroupBy(f => Prototype(f)?.ID))
        {
            var list = group.ToList();
            var over = list.Count > list[0].Comp.StablePopulation;
            foreach (var fish in list)
                fish.Comp.Overpopulated = over;
        }
    }

    private void OnFishStatusChanged(Entity<ImperialAquariumComponent> ent, ref ImperialFishInContainerStatusChangedEvent args)
    {
        if (ent.Comp.Mode == ImperialAquariumMode.Auto)
            GetOptimalSettings(ent);
        UpdateUi(ent);
    }

    private void OnEnvironmentCheck(Entity<ImperialAquariumComponent> ent, ref ImperialFishEnvironmentCheckEvent args)
    {
        args.Handled = true;
        args.Proper = args.Fish.Comp.SafeFluid && args.Fish.Comp.SafeTemperature;
    }

    private void OnGrowthCheck(Entity<ImperialAquariumComponent> ent, ref ImperialFishGrowthCheckEvent args)
    {
        if (ent.Comp.StopGrowth || ent.Comp.Cramped)
            args.Stopped = true;
    }

    /// <summary>is_fish_population_safe(): опасность грозит не более чем половине живых рыб.</summary>
    private bool IsPopulationSafe(Entity<ImperialAquariumComponent> ent)
    {
        var alive = 0;
        var notSafe = 0;
        foreach (var fish in GetFishes(ent))
        {
            if (fish.Comp.Dead)
                continue;
            if (!_fish.ProperEnvironment(fish) || _fish.GetHunger(fish.Comp) > ImperialFishing.StarvingThreshold * 0.9f)
                notSafe++;
            alive++;
        }

        return notSafe <= alive / 2f;
    }

    /// <summary>
    /// get_optimal_aquarium_settings(): подбирает среду и температуру, при которых выживет больше всего рыб,
    /// и включает стазис, если в опасности больше половины.
    /// </summary>
    private void GetOptimalSettings(Entity<ImperialAquariumComponent> ent)
    {
        var comp = ent.Comp;
        var byType = GetFishes(ent)
            .GroupBy(f => Prototype(f)?.ID ?? string.Empty)
            .OrderByDescending(g => g.Count())
            .ToList();

        if (byType.Count == 0)
        {
            comp.StopGrowth = false;
            Dirty(ent);
            return;
        }

        var data = new Dictionary<string, ImperialFishComponent>();
        foreach (var group in byType)
            data[group.Key] = _data.GetFish(group.Key) ?? group.First().Comp;

        var canSurviveWith = new Dictionary<string, HashSet<string>>();
        var aliveByType = new Dictionary<string, int>();
        var checkedTypes = new List<string>();
        foreach (var group in byType)
        {
            var type = group.Key;
            var ours = data[type];
            checkedTypes.Add(type);
            if (!canSurviveWith.ContainsKey(type))
                canSurviveWith[type] = new HashSet<string>();

            var amount = group.Count(f => !f.Comp.Dead);
            if (amount == 0)
                continue;

            aliveByType[type] = amount;
            foreach (var enemy in byType.Select(g => g.Key).Except(checkedTypes))
            {
                var theirs = data[enemy];
                if (ours.TemperatureMin > theirs.TemperatureMax || ours.TemperatureMax < theirs.TemperatureMin)
                    continue;
                if (!CompatibleFluids(ours.RequiredFluid).Intersect(CompatibleFluids(theirs.RequiredFluid)).Any())
                    continue;

                canSurviveWith[type].Add(enemy);
                if (!canSurviveWith.TryGetValue(enemy, out var enemySet))
                    canSurviveWith[enemy] = enemySet = new HashSet<string>();
                enemySet.Add(type);
            }
        }

        var highest = new List<string> { byType[0].Key };
        var highestVal = -1;
        foreach (var (type, compatible) in canSurviveWith)
        {
            // Как в TG: живые рыбы вида считаются дважды.
            var amount = aliveByType.GetValueOrDefault(type) * 2;
            foreach (var ally in compatible)
            {
                var val = amount + aliveByType.GetValueOrDefault(ally);
                var common = compatible.Intersect(canSurviveWith.GetValueOrDefault(ally) ?? new HashSet<string>()).ToList();
                foreach (var third in common)
                    val += aliveByType.GetValueOrDefault(third);
                if (val <= highestVal)
                    continue;

                highest = new List<string> { type, ally };
                highest.AddRange(common);
                highestVal = val;
            }
        }

        var minTemp = ImperialFishing.MinAquariumTemp;
        var maxTemp = ImperialFishing.MaxAquariumTemp;
        var fluids = FluidTypes.ToList();
        foreach (var type in highest)
        {
            var fish = data[type];
            fluids = fluids.Intersect(CompatibleFluids(fish.RequiredFluid)).ToList();
            minTemp = MathF.Max(minTemp, fish.TemperatureMin);
            maxTemp = MathF.Min(maxTemp, fish.TemperatureMax);
        }

        comp.Temperature = Math.Clamp(minTemp + MathF.Round((maxTemp - minTemp) / 2), comp.MinTemperature, comp.MaxTemperature);
        SetFluid(ent, fluids.Count > 0 ? fluids[0] : comp.Fluid);
        comp.StopGrowth = !IsPopulationSafe(ent);
        Dirty(ent);
    }

    /// <summary>set_aquarium_mode().</summary>
    public void SetMode(Entity<ImperialAquariumComponent> ent, ImperialAquariumMode mode)
    {
        if (ent.Comp.Mode == mode)
            return;

        ent.Comp.StopGrowth = false;
        ent.Comp.Mode = mode;
        switch (mode)
        {
            case ImperialAquariumMode.Safe:
                ent.Comp.StopGrowth = true;
                break;
            case ImperialAquariumMode.Auto:
                GetOptimalSettings(ent);
                break;
        }

        Dirty(ent);
    }

    /// <summary>set_fluid_type().</summary>
    public void SetFluid(Entity<ImperialAquariumComponent> ent, ImperialFishFluid fluid)
    {
        ent.Comp.Fluid = fluid;
        CheckAllFish(ent);
        UpdateAppearance(ent);
        Dirty(ent);
    }

    private void UpdateAppearance(Entity<ImperialAquariumComponent> ent)
    {
        _appearance.SetData(ent, ImperialAquariumVisuals.PanelOpen, ent.Comp.PanelOpen);
        _appearance.SetData(ent, ImperialAquariumVisuals.Air, ent.Comp.Fluid == ImperialFishFluid.Air);
        _appearance.SetData(ent, ImperialAquariumVisuals.Broken, ent.Comp.Broken);
    }

    #endregion

    #region Вставка, корм, панель

    private void OnInteractUsing(Entity<ImperialAquariumComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;
        var user = args.User;

        // Банка корма: при открытой панели — пополнить хранилище, иначе покормить рыб.
        if (HasComp<ImperialFishFeedComponent>(used))
        {
            args.Handled = true;
            if (!_solution.TryGetDrainableSolution(used, out var canSoln, out var canSolution) || canSolution.Volume <= 0)
            {
                _popup.PopupEntity(Loc.GetString("imperial-aquarium-feed-empty", ("item", used)), ent, user);
                return;
            }

            if (ent.Comp.PanelOpen && _solution.TryGetSolution(ent.Owner, ImperialAquariumComponent.FeedSolution, out var feedSoln, out var feed))
            {
                var amount = FixedPoint2Min(canSolution.Volume, feed.AvailableVolume, 2.5f);
                if (amount <= 0)
                {
                    _popup.PopupEntity(Loc.GetString("imperial-aquarium-feed-full"), ent, user);
                    return;
                }

                _solution.TryAddSolution(feedSoln.Value, _solution.SplitSolution(canSoln.Value, amount));
                _popup.PopupEntity(Loc.GetString("imperial-aquarium-feed-stored"), ent, user);
                return;
            }

            var fishes = GetFishes(ent);
            if (fishes.Count == 0)
            {
                _popup.PopupEntity(Loc.GetString("imperial-aquarium-no-fish"), ent, user);
                return;
            }

            FeedFishes(ent, fishes, canSoln.Value);
            _popup.PopupEntity(Loc.GetString("imperial-aquarium-fed"), ent, user);
            return;
        }

        // Ремонт стекла двумя листами.
        if (TryComp<StackComponent>(used, out var stack) && stack.StackTypeId == "Glass" && ent.Comp.Broken)
        {
            args.Handled = true;
            if (_stack.GetCount((used, stack)) < 2)
            {
                _popup.PopupEntity(Loc.GetString("imperial-aquarium-repair-need"), ent, user);
                return;
            }

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(2), new ImperialAquariumRepairDoAfterEvent(), ent, ent, used)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
            return;
        }

        if (!HasComp<ImperialFishComponent>(used) && !HasComp<ImperialAquariumPropComponent>(used))
            return;

        // В стационарный аквариум не кладём в боевом режиме.
        if (!ent.Comp.Portable && _combat.IsInCombatMode(user))
            return;

        args.Handled = true;
        if (!CanInsert(ent, used, user))
            return;

        if (ent.Comp.Broken)
        {
            _popup.PopupEntity(Loc.GetString("imperial-aquarium-broken"), ent, user);
            return;
        }

        var container = _container.EnsureContainer<Container>(ent, ImperialAquariumComponent.ContainerId);
        if (!_container.Insert(used, container))
        {
            _popup.PopupEntity(Loc.GetString("imperial-aquarium-stuck"), ent, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-aquarium-added"), ent, user);
    }

    private static float FixedPoint2Min(Content.Shared.FixedPoint.FixedPoint2 a, Content.Shared.FixedPoint.FixedPoint2 b, float c)
    {
        return MathF.Min(MathF.Min(a.Float(), b.Float()), c);
    }

    /// <summary>can_insert().</summary>
    private bool CanInsert(Entity<ImperialAquariumComponent> ent, EntityUid item, EntityUid user)
    {
        if (TryComp<ImperialFishComponent>(item, out var fish))
        {
            if (ent.Comp.MaxFishSize is { } maxSize && fish.Size > maxSize)
            {
                _popup.PopupEntity(Loc.GetString("imperial-aquarium-fish-too-big"), ent, user);
                return false;
            }

            if (ent.Comp.MaxTotalSize is { } maxTotal && ent.Comp.SummedSize > maxTotal)
            {
                _popup.PopupEntity(Loc.GetString("imperial-aquarium-tank-full"), ent, user);
                return false;
            }

            return true;
        }

        if (TryComp<ImperialAquariumPropComponent>(item, out var prop) && prop.Unique
            && _container.TryGetContainer(ent, ImperialAquariumComponent.ContainerId, out var container))
        {
            var proto = Prototype(item)?.ID;
            if (container.ContainedEntities.Any(e => e != item && Prototype(e)?.ID == proto))
            {
                _popup.PopupEntity(Loc.GetString("imperial-aquarium-cannot-add"), ent, user);
                return false;
            }
        }

        return true;
    }

    /// <summary>feed_fishes().</summary>
    private void FeedFishes(Entity<ImperialAquariumComponent> ent, List<Entity<ImperialFishComponent>> fishes, Entity<SolutionComponent> solution)
    {
        foreach (var fish in fishes)
            _breeding.Feed(fish, solution);

        if (ent.Comp.Mode == ImperialAquariumMode.Auto && IsPopulationSafe(ent))
        {
            ent.Comp.StopGrowth = false;
            Dirty(ent);
        }
    }

    private void OnAltVerbs(Entity<ImperialAquariumComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.PanelOpen ? "imperial-aquarium-verb-close-panel" : "imperial-aquarium-verb-open-panel"),
            Act = () => TogglePanel(ent, user),
        });
    }

    private void OnVerbs(Entity<ImperialAquariumComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("imperial-aquarium-verb-admire"),
            Act = () => StartAdmire(ent, user),
        });

        if (ent.Comp.PanelOpen && _solution.TryGetSolution(ent.Owner, ImperialAquariumComponent.FeedSolution, out _, out var feed) && feed.Volume > 0)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("imperial-aquarium-verb-flush"),
                Act = () => FlushFeed(ent, user),
            });
        }
    }

    public void TogglePanel(Entity<ImperialAquariumComponent> ent, EntityUid user)
    {
        ent.Comp.PanelOpen = !ent.Comp.PanelOpen;
        if (!ent.Comp.PanelOpen)
            _ui.CloseUi(ent.Owner, ImperialAquariumUiKey.Key);

        _popup.PopupEntity(Loc.GetString(ent.Comp.PanelOpen ? "imperial-aquarium-panel-opened" : "imperial-aquarium-panel-closed"), ent, user);
        UpdateAppearance(ent);
        Dirty(ent);
    }

    /// <summary>Вантуз в TG: слить хранилище корма на пол.</summary>
    private void FlushFeed(Entity<ImperialAquariumComponent> ent, EntityUid user)
    {
        if (!_solution.TryGetSolution(ent.Owner, ImperialAquariumComponent.FeedSolution, out var feedSoln, out var feed))
            return;

        var spilled = _solution.SplitSolution(feedSoln.Value, feed.Volume);
        _puddle.TrySpillAt(ent, spilled, out _);
        _popup.PopupEntity(Loc.GetString("imperial-aquarium-flushed"), ent, user);
    }

    private void OnActivate(Entity<ImperialAquariumComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || ent.Comp.Portable)
            return;

        args.Handled = true;
        Interact(ent, args.User);
    }

    private void OnUseInHand(Entity<ImperialAquariumComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Interact(ent, args.User);
    }

    private void Interact(Entity<ImperialAquariumComponent> ent, EntityUid user)
    {
        if (ent.Comp.PanelOpen)
        {
            _ui.OpenUi(ent.Owner, ImperialAquariumUiKey.Key, user);
            return;
        }

        StartAdmire(ent, user);
    }

    private void StartAdmire(Entity<ImperialAquariumComponent> ent, EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("imperial-aquarium-admiring"), ent, user);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(5), new ImperialAquariumAdmireDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
        });
    }

    /// <summary>admire(): живые рыбы радуют, мёртвые огорчают.</summary>
    private void OnAdmireDoAfter(Entity<ImperialAquariumComponent> ent, ref ImperialAquariumAdmireDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var fishes = GetFishes(ent);
        var alive = fishes.Count(f => !f.Comp.Dead);
        var dead = fishes.Count - alive;
        if (alive > 0)
            _popup.PopupEntity(Loc.GetString("imperial-aquarium-admire-positive"), ent, args.User);
        else if (dead > 0)
            _popup.PopupEntity(Loc.GetString("imperial-aquarium-admire-negative"), ent, args.User, PopupType.SmallCaution);
        else
            _popup.PopupEntity(Loc.GetString("imperial-aquarium-admire-empty"), ent, args.User);
    }

    private void OnExamined(Entity<ImperialAquariumComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-aquarium-examine-settings",
            ("temperature", MathF.Round(ent.Comp.Temperature, 1)),
            ("fluid", Loc.GetString($"imperial-aquarium-fluid-{ent.Comp.Fluid.ToString().ToLowerInvariant()}"))));
        args.PushMarkup(Loc.GetString(ent.Comp.PanelOpen ? "imperial-aquarium-examine-panel-open" : "imperial-aquarium-examine-panel-closed"));
        if (ent.Comp.Cramped)
            args.PushMarkup(Loc.GetString("imperial-aquarium-examine-cramped"));
        if (ent.Comp.Broken)
            args.PushMarkup(Loc.GetString("imperial-aquarium-examine-broken"));
        if (ent.Comp.BioelectricGenerator)
            args.PushMarkup(Loc.GetString("imperial-aquarium-examine-bioelectric"));
    }

    #endregion

    #region Поломка и ремонт

    private void OnDamageChanged(Entity<ImperialAquariumComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Broken || ent.Comp.Portable || _damageable.GetTotalDamage(ent.Owner) < BreakDamage)
            return;

        Smash(ent);
    }

    /// <summary>aquarium_smash(): содержимое высыпается рядом, вода разливается.</summary>
    private void Smash(Entity<ImperialAquariumComponent> ent)
    {
        ent.Comp.Broken = true;
        _audio.PlayPvs("/Audio/Effects/glass_break3.ogg", ent);
        if (_container.TryGetContainer(ent, ImperialAquariumComponent.ContainerId, out var container))
        {
            foreach (var item in container.ContainedEntities.ToList())
            {
                _container.Remove(item, container);
                var offset = new System.Numerics.Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f));
                _xform.SetCoordinates(item, Transform(ent).Coordinates.Offset(offset));
            }
        }

        if (ent.Comp.Fluid != ImperialFishFluid.Air)
            _puddle.TrySpillAt(ent, new Solution("Water", 30), out _);

        UpdateAppearance(ent);
        Dirty(ent);
    }

    private void OnRepairDoAfter(Entity<ImperialAquariumComponent> ent, ref ImperialAquariumRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used || !ent.Comp.Broken)
            return;

        if (!_stack.TryUse(used, 2))
            return;

        args.Handled = true;
        ent.Comp.Broken = false;
        _damageable.ClearAllDamage(ent.Owner);
        UpdateAppearance(ent);
        Dirty(ent);
    }

    #endregion

    #region Улучшения

    private void OnUpgradeInteract(Entity<ImperialAquariumUpgradeComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<ImperialAquariumComponent>(target))
            return;

        args.Handled = true;
        if (Prototype(target)?.ID != ent.Comp.From)
        {
            _popup.PopupEntity(Loc.GetString("imperial-aquarium-upgrade-wrong"), target, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-aquarium-upgrading"), target, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(5), new ImperialAquariumUpgradeDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    /// <summary>TransferComponents(): рыбы, корм и настройки переходят в новый аквариум.</summary>
    private void OnUpgradeDoAfter(Entity<ImperialAquariumUpgradeComponent> ent, ref ImperialAquariumUpgradeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !TryComp<ImperialAquariumComponent>(target, out var old))
            return;

        args.Handled = true;
        var coords = Transform(target).Coordinates;
        var upgraded = Spawn(ent.Comp.To, coords);
        if (!TryComp<ImperialAquariumComponent>(upgraded, out var comp))
            return;

        var newEnt = (upgraded, comp);
        comp.Temperature = Math.Clamp(old.Temperature, comp.MinTemperature, comp.MaxTemperature);
        comp.FeedingInterval = old.FeedingInterval;
        comp.LastFeeding = old.LastFeeding;
        SetMode(newEnt, old.Mode);
        SetFluid(newEnt, old.Fluid);

        var newContainer = _container.EnsureContainer<Container>(upgraded, ImperialAquariumComponent.ContainerId);
        if (_container.TryGetContainer(target, ImperialAquariumComponent.ContainerId, out var oldContainer))
        {
            foreach (var item in oldContainer.ContainedEntities.ToList())
            {
                _container.Remove(item, oldContainer);
                _container.Insert(item, newContainer);
            }
        }

        if (_solution.TryGetSolution(target, ImperialAquariumComponent.FeedSolution, out var oldFeed, out var oldSolution)
            && _solution.TryGetSolution(upgraded, ImperialAquariumComponent.FeedSolution, out var newFeed, out _))
            _solution.TryAddSolution(newFeed.Value, _solution.SplitSolution(oldFeed.Value, oldSolution.Volume));

        if (_hands.IsHolding(args.User, target))
            _hands.TryPickupAnyHand(args.User, upgraded);

        _popup.PopupEntity(Loc.GetString("imperial-aquarium-upgraded"), upgraded, args.User);
        QueueDel(target);
        QueueDel(ent);
    }

    #endregion

    #region Ловля, биогенератор

    /// <summary>/datum/fish_source/aquarium: ловится любая живая рыба из аквариума.</summary>
    private void OnRollReward(Entity<ImperialAquariumComponent> ent, ref ImperialFishingRollRewardEvent args)
    {
        var alive = GetFishes(ent).Where(f => !f.Comp.Dead).ToList();
        if (alive.Count == 0)
            return;

        args.RewardEntity = _random.Pick(alive).Owner;
    }

    /// <summary>TRAIT_BIOELECTRIC_GENERATOR: электрогенез рыб усиливается и бьёт молниями в теслы.</summary>
    private void OnElectrogenesis(Entity<ImperialAquariumComponent> ent, ref ImperialFishElectrogenesisEvent args)
    {
        if (!ent.Comp.BioelectricGenerator)
            return;

        args.Handled = true;
        var bolts = Math.Clamp((int) MathF.Ceiling(args.Power / 5_000_000f), 1, 3);
        _lightning.ShootRandomLightnings(ent, 5f, bolts);
    }

    #endregion

    #region Интерфейс

    private void OnSetTemperature(Entity<ImperialAquariumComponent> ent, ref ImperialAquariumSetTemperatureMessage args)
    {
        if (ent.Comp.Mode == ImperialAquariumMode.Auto || !float.IsFinite(args.Temperature))
            return;

        ent.Comp.Temperature = Math.Clamp(args.Temperature, ent.Comp.MinTemperature, ent.Comp.MaxTemperature);
        CheckAllFish(ent);
        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnSetFluid(Entity<ImperialAquariumComponent> ent, ref ImperialAquariumSetFluidMessage args)
    {
        if (ent.Comp.Mode == ImperialAquariumMode.Auto || !FluidTypes.Contains(args.Fluid))
            return;

        SetFluid(ent, args.Fluid);
        UpdateUi(ent);
    }

    private void OnSetMode(Entity<ImperialAquariumComponent> ent, ref ImperialAquariumSetModeMessage args)
    {
        SetMode(ent, args.Mode);
        UpdateUi(ent);
    }

    private void OnSetFeedingInterval(Entity<ImperialAquariumComponent> ent, ref ImperialAquariumSetFeedingIntervalMessage args)
    {
        if (!float.IsFinite(args.Minutes))
            return;

        ent.Comp.FeedingInterval = TimeSpan.FromMinutes(Math.Clamp(args.Minutes, 1, 7));
        Dirty(ent);
        UpdateUi(ent);
    }

    private bool TryGetContent(Entity<ImperialAquariumComponent> ent, NetEntity net, out EntityUid uid)
    {
        uid = GetEntity(net);
        return _container.TryGetContainer(ent, ImperialAquariumComponent.ContainerId, out var container) && container.Contains(uid);
    }

    private void OnPetFish(Entity<ImperialAquariumComponent> ent, ref ImperialAquariumPetFishMessage args)
    {
        if (TryGetContent(ent, args.Fish, out var uid) && TryComp<ImperialFishComponent>(uid, out var fish))
            _fish.TryPet((uid, fish), args.Actor, true);
        UpdateUi(ent);
    }

    private void OnRemoveItem(Entity<ImperialAquariumComponent> ent, ref ImperialAquariumRemoveItemMessage args)
    {
        if (!TryGetContent(ent, args.Item, out var uid))
            return;

        _container.TryRemoveFromContainer(uid);
        _hands.TryPickupAnyHand(args.Actor, uid);
        _popup.PopupEntity(Loc.GetString("imperial-aquarium-taken-out", ("item", uid)), ent, args.Actor);
        UpdateUi(ent);
    }

    private void OnRenameFish(Entity<ImperialAquariumComponent> ent, ref ImperialAquariumRenameFishMessage args)
    {
        if (!TryGetContent(ent, args.Fish, out var uid))
            return;

        var name = args.Name.Trim();
        if (name.Length == 0 || name.Length > 32 || name == Name(uid))
            return;

        _meta.SetEntityName(uid, name);
        UpdateUi(ent);
    }

    /// <summary>get_happiness_value(): 0..4.</summary>
    private int GetHappiness(Entity<ImperialFishComponent> fish)
    {
        var value = 0;
        if (_timing.CurTime < fish.Comp.PettedUntil)
            value++;
        if (_fish.GetHunger(fish.Comp) < 0.5f)
            value++;
        if (fish.Comp.SafeFluid)
            value++;
        if (fish.Comp.SafeTemperature)
            value++;
        if (fish.Comp.BitesAmount > 0)
            value -= 2;
        if (ImperialFishSystem.GetHealthPercentage(fish.Comp) < 0.6f)
            value--;
        return Math.Clamp(value, 0, 4);
    }

    public void UpdateUi(Entity<ImperialAquariumComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, ImperialAquariumUiKey.Key))
            return;

        var state = new ImperialAquariumBoundUserInterfaceState
        {
            Fluid = ent.Comp.Fluid,
            Temperature = ent.Comp.Temperature,
            MinTemperature = ent.Comp.MinTemperature,
            MaxTemperature = ent.Comp.MaxTemperature,
            Mode = ent.Comp.Mode,
            FeedingIntervalMinutes = (float) ent.Comp.FeedingInterval.TotalMinutes,
        };

        if (_container.TryGetContainer(ent, ImperialAquariumComponent.ContainerId, out var container))
        {
            foreach (var item in container.ContainedEntities)
            {
                if (TryComp<ImperialFishComponent>(item, out var fish))
                {
                    state.Fish.Add(new ImperialAquariumFishEntry
                    {
                        Entity = GetNetEntity(item),
                        Proto = Prototype(item)?.ID,
                        Name = Name(item),
                        Alive = !fish.Dead,
                        Happiness = GetHappiness((item, fish)),
                    });
                    continue;
                }

                state.Props.Add(new ImperialAquariumPropEntry { Entity = GetNetEntity(item), Proto = Prototype(item)?.ID, Name = Name(item) });
            }
        }

        _ui.SetUiState(ent.Owner, ImperialAquariumUiKey.Key, state);
    }

    #endregion

    #region Обновление

    /// <summary>Суммарный размер и вес рыб переносного аквариума: теснота, замедление.</summary>
    private void UpdatePortable(Entity<ImperialAquariumComponent> ent)
    {
        if (ent.Comp.MaxTotalSize is not { } maxTotal)
            return;

        var fishes = GetFishes(ent);
        ent.Comp.SummedSize = fishes.Sum(f => f.Comp.Size);
        ent.Comp.SummedWeight = fishes.Sum(f => f.Comp.Weight);
        var cramped = ent.Comp.SummedSize > maxTotal;
        if (cramped != ent.Comp.Cramped)
        {
            ent.Comp.Cramped = cramped;
            Dirty(ent);
        }

        var slowdown = ent.Comp.SummedWeight < ImperialFishing.WeightSlowdown
            ? 0
            : ImperialFishing.GetSlowdown(ent.Comp.SummedWeight) * ent.Comp.SlowdownCoeff;
        _slowdown.SetSlowdown(ent, slowdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        var dt = _accumulator;
        _accumulator = 0;
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<ImperialAquariumComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);

            // Безопасный режим: рыбы не голодают, корм не тратится.
            if (comp.StopGrowth && comp.Mode == ImperialAquariumMode.Safe)
            {
                comp.LastFeeding += TimeSpan.FromSeconds(dt);
            }
            else if (now >= comp.LastFeeding + comp.FeedingInterval
                     && _solution.TryGetSolution(uid, ImperialAquariumComponent.FeedSolution, out var feedSoln, out var feed)
                     && feed.Volume > 0)
            {
                comp.LastFeeding = now;
                FeedFishes(ent, GetFishes(uid), feedSoln.Value);
            }

            UpdatePortable(ent);
            UpdateUi(ent);
        }
    }

    #endregion
}
