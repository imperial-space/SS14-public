using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Жизнь рыбы (/obj/item/fish): размер и вес, здоровье, голод, смерть вне воды, осмотр, разделка.
/// </summary>
public sealed partial class ImperialFishSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ImperialFishingSkillSystem _skill = default!;
    [Dependency] private readonly ImperialFishTraitSystem _traits = default!;
    [Dependency] private readonly ImperialFishDataSystem _data = default!;
    [Dependency] private readonly ImperialFishingSpotSystem _spots = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ImperialSpecialFishSystem _special = default!;
    [Dependency] private readonly ImperialFishingSlowdownSystem _slowdown = default!;

    private static readonly ProtoId<ToolQualityPrototype> SlicingQuality = "Slicing";
    public const string FoodSolution = "food";

    /// <summary>FISH_SUBMERGING_THRESHOLD.</summary>
    private const float SubmergingThreshold = 100f;

    /// <summary>STARVING_FISH_SUBMERGING_THRESHOLD.</summary>
    private const float StarvingSubmergingThreshold = 20f;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialFishComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ImperialFishComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialFishComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ImperialFishComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ImperialFishComponent, ImperialFishFilletDoAfterEvent>(OnFilletDoAfter);
        SubscribeLocalEvent<ImperialFishComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
    }

    private void OnMapInit(Entity<ImperialFishComponent> ent, ref MapInitEvent args)
    {
        var now = _timing.CurTime;
        ent.Comp.BreedingWait = now + ent.Comp.BreedingTimeout * ImperialFishing.NewFishBreedingTimeoutMult;
        ent.Comp.LastFeeding = now - ent.Comp.FeedingFrequency * ImperialFishing.NewFishLastFeedingMult;

        if (ent.Comp.Traits.Count == 0)
        {
            ent.Comp.Traits = ent.Comp.InitialTraits.ToList();
            if (MetaData(ent).EntityPrototype is { } proto)
            {
                foreach (var (trait, chance) in _data.GetSpontaneousTraitChances(proto.ID))
                {
                    if (_random.Prob(chance / 100f) && !ent.Comp.Traits.Contains(trait))
                        ent.Comp.Traits.Add(trait);
                }
            }
        }

        _traits.ApplyAll(ent);

        if (ent.Comp.Size <= 0)
            UpdateSizeAndWeight(ent, ent.Comp.AverageSize, ent.Comp.AverageWeight);
        else
            UpdateSizeAndWeight(ent, ent.Comp.Size, ent.Comp.Weight);

        Dirty(ent);
        _special.OnFishMapInit(ent);
    }

    #region Размер и вес

    /// <summary>randomize_size_and_weight(): нормальное распределение вокруг средних значений.</summary>
    public void RandomizeSizeAndWeight(Entity<ImperialFishComponent> ent, float? baseSize = null, float? baseWeight = null, bool update = true, float deviation = 0.2f)
    {
        var comp = ent.Comp;
        var size = baseSize ?? comp.AverageSize;
        var weight = baseWeight ?? comp.AverageWeight;

        comp.TempSize = MathF.Round(Math.Clamp(Gaussian(size, deviation * size), comp.AverageSize / ImperialFishing.MaxDeviationCoeff, comp.AverageSize * ImperialFishing.MaxDeviationCoeff));
        comp.TempWeight = MathF.Round(Math.Clamp(Gaussian(weight, deviation * weight), comp.AverageWeight / ImperialFishing.MaxDeviationCoeff, comp.AverageWeight * ImperialFishing.MaxDeviationCoeff));

        SetMaxSizeAndWeight(comp, comp.TempSize, comp.TempWeight);
        if (update)
            UpdateSizeAndWeight(ent, comp.TempSize, comp.TempWeight);
    }

    private float Gaussian(float mean, float deviation)
    {
        // Бокс — Мюллер.
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        var normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return (float) (mean + deviation * normal);
    }

    private static void SetMaxSizeAndWeight(ImperialFishComponent comp, float baseSize, float baseWeight)
    {
        if (comp.MaximumSize <= 0)
            comp.MaximumSize = MathF.Min(baseSize * 2, comp.AverageSize * ImperialFishing.MaxDeviationCoeff);
        if (comp.MaximumWeight <= 0)
            comp.MaximumWeight = MathF.Min(baseWeight * 2, comp.AverageWeight * ImperialFishing.MaxDeviationCoeff);
    }

    /// <summary>update_size_and_weight(): размер предмета, съедобность.</summary>
    public void UpdateSizeAndWeight(Entity<ImperialFishComponent> ent, float newSize, float newWeight)
    {
        var comp = ent.Comp;
        SetMaxSizeAndWeight(comp, newSize, newWeight);

        comp.Size = newSize;
        var itemSize = newSize switch
        {
            <= ImperialFishing.SizeTinyMax => "Tiny",
            <= ImperialFishing.SizeSmallMax => "Small",
            <= ImperialFishing.SizeNormalMax => "Normal",
            <= ImperialFishing.SizeBulkyMax => "Large",
            <= ImperialFishing.SizeHugeMax => "Huge",
            _ => "Ginormous",
        };

        if (HasComp<ItemComponent>(ent))
            _item.SetSize(ent, itemSize);

        UpdateHeldPrefix(ent);

        var oldWeight = comp.Weight;
        comp.Weight = newWeight;
        RegenerateReagents(ent, oldWeight);
        _slowdown.SetSlowdown(ent, newWeight >= ImperialFishing.WeightSlowdown ? ImperialFishing.GetSlowdown(newWeight) : 0);
        Dirty(ent);

        var sizeEv = new ImperialFishSizeUpdatedEvent();
        RaiseLocalEvent(ent, ref sizeEv);
    }

    /// <summary>inhand_icon_state: fish_small … fish_huge, _wielded у самых крупных.</summary>
    public void UpdateHeldPrefix(Entity<ImperialFishComponent> ent)
    {
        if (!HasComp<ItemComponent>(ent))
            return;

        var size = ent.Comp.Size;
        var prefix = ent.Comp.InhandState ?? size switch
        {
            <= ImperialFishing.SizeSmallMax => "small",
            <= ImperialFishing.SizeNormalMax => "normal",
            <= ImperialFishing.SizeBulkyMax => "bulky",
            _ => "huge",
        };

        if (ent.Comp.InhandState == "chainsawfish" && ent.Comp.Dead)
            prefix = "chainsawfish-dead";

        if (size > ImperialFishing.SizeTwoHandsRequired && (prefix == "huge" || ent.Comp.InhandState != null))
            prefix += "-wielded";

        _item.SetHeldPrefix(ent, prefix);
    }

    /// <summary>Сколько укусов в рыбе (weight / FISH_WEIGHT_BITE_DIVISOR).</summary>
    public static float BitesToFinish(ImperialFishComponent comp) => MathF.Max(comp.Weight / ImperialFishing.WeightBiteDivisor, 0.1f);

    /// <summary>generate_fish_reagents(): белок, кровь, соль и реагенты черт.</summary>
    private void RegenerateReagents(Entity<ImperialFishComponent> ent, float oldWeight)
    {
        if (!_solution.EnsureSolutionEntity(ent.Owner, FoodSolution, out var soln))
            return;

        var bitesLeft = MathF.Max(BitesToFinish(ent.Comp) - ent.Comp.BitesAmount, 0.1f);
        var reagents = GetEdibleReagents(ent);

        var solution = soln.Value.Comp.Solution;
        solution.RemoveAllSolution();
        var total = 0f;
        foreach (var (_, amount) in reagents)
            total += amount * bitesLeft;

        solution.MaxVolume = FixedPoint2.New(MathF.Max(total * 1.25f, 1f));
        foreach (var (reagent, amount) in reagents)
        {
            if (amount * bitesLeft > 0)
                solution.AddReagent(reagent, FixedPoint2.New(amount * bitesLeft));
        }

        _solution.UpdateChemicals(soln.Value);
    }

    /// <summary>get_base_edible_reagents_to_add() + реагенты черт.</summary>
    public Dictionary<string, float> GetEdibleReagents(Entity<ImperialFishComponent> ent)
    {
        var comp = ent.Comp;
        var result = new Dictionary<string, float>();
        if (comp.Cooked)
        {
            result["Protein"] = 4;
        }
        else
        {
            result["UncookedAnimalProteins"] = 2;
            result["Blood"] = 1;
        }

        if (comp.RequiredFluid == ImperialFishFluid.Saltwater)
            result["TableSalt"] = 0.4f;

        _traits.AddTraitReagents(ent, result);
        return result;
    }

    #endregion

    #region Здоровье и голод

    /// <summary>get_health_percentage(): 0..1 до смерти.</summary>
    public static float GetHealthPercentage(ImperialFishComponent comp)
    {
        var maxHealth = comp.MaxIntegrity * (1 - comp.IntegrityFailure);
        var deathThreshold = comp.MaxIntegrity - maxHealth;
        return Math.Clamp((comp.Integrity - deathThreshold) / maxHealth, 0, 1);
    }

    /// <summary>damage_fish(): урон живой рыбе, смерть на пороге.</summary>
    public void DamageFish(Entity<ImperialFishComponent> ent, float amount)
    {
        if (ent.Comp.Dead || amount <= 0)
            return;

        var threshold = ent.Comp.MaxIntegrity * ent.Comp.IntegrityFailure;
        ent.Comp.Integrity = MathF.Max(ent.Comp.Integrity - amount, threshold);
        if (ent.Comp.Integrity <= threshold)
            SetDead(ent, true);
    }

    public void RepairFish(Entity<ImperialFishComponent> ent, float amount)
    {
        if (amount <= 0)
            return;

        ent.Comp.Integrity = MathF.Min(ent.Comp.Integrity + amount, ent.Comp.MaxIntegrity);
    }

    /// <summary>set_status().</summary>
    public void SetDead(Entity<ImperialFishComponent> ent, bool dead, bool silent = false)
    {
        if (ent.Comp.Dead == dead)
            return;

        ent.Comp.Dead = dead;
        if (!dead)
        {
            ent.Comp.Integrity = ent.Comp.MaxIntegrity;
            ent.Comp.BitesAmount = 0;
            ent.Comp.LastFeeding = _timing.CurTime;
            RegenerateReagents(ent, ent.Comp.Weight);
        }
        else if (!silent)
        {
            var message = Loc.GetString(ent.Comp.DeathText, ("fish", ent.Owner));
            var target = _container.TryGetContainingContainer(ent.Owner, out var container) ? container.Owner : ent.Owner;
            _popup.PopupEntity(message, target, PopupType.SmallCaution);
        }

        UpdateHeldPrefix(ent);
        Dirty(ent);
        var ev = new ImperialFishStatusChangedEvent(dead);
        RaiseLocalEvent(ent, ref ev);
        _special.OnFishStatusChanged(ent, dead);
        if (_container.TryGetContainingContainer(ent.Owner, out var holder))
        {
            var holderEv = new ImperialFishInContainerStatusChangedEvent(ent, dead);
            RaiseLocalEvent(holder.Owner, ref holderEv);
        }
    }

    /// <summary>get_hunger(): 0..cap.</summary>
    public float GetHunger(ImperialFishComponent comp, float cap = ImperialFishing.StarvingThreshold)
    {
        var hunger = Math.Clamp((float) ((_timing.CurTime - comp.LastFeeding).TotalSeconds / comp.FeedingFrequency.TotalSeconds), 0, cap);
        if (_traits.Has(comp, "Mixotroph"))
            return MathF.Min(hunger, ImperialFishing.StarvingThreshold * 0.2f);

        return hunger;
    }

    public float GetStarvationMult(ImperialFishComponent comp)
    {
        var hunger = GetHunger(comp, ImperialFishing.StarvingThreshold * 2);
        return hunger >= ImperialFishing.StarvingThreshold ? hunger : 0;
    }

    /// <summary>
    /// proper_environment(): рыба в аквариуме, на своём месте ловли или (для дышащих воздухом) в нормальном воздухе.
    /// </summary>
    public bool ProperEnvironment(Entity<ImperialFishComponent> ent)
    {
        if (_container.TryGetContainingContainer(ent.Owner, out var container))
        {
            var ev = new ImperialFishEnvironmentCheckEvent(ent);
            RaiseLocalEvent(container.Owner, ref ev);
            if (ev.Handled)
                return ev.Proper;

            // В рюкзаке и руках вода не нужна только воздушной рыбе.
            return ent.Comp.RequiredFluid == ImperialFishFluid.Air || _traits.Has(ent.Comp, "Amphibious") ? AirIsSafe(ent) : false;
        }

        if (_spots.IsOnSafeSpot(ent))
            return true;

        if (ent.Comp.RequiredFluid != ImperialFishFluid.Air && !_traits.Has(ent.Comp, "Amphibious"))
            return false;

        return AirIsSafe(ent);
    }

    private bool AirIsSafe(Entity<ImperialFishComponent> ent)
    {
        var mixture = _atmos.GetContainingMixture(ent.Owner, excite: false);
        if (mixture == null)
            return false;

        if (mixture.Temperature < ent.Comp.TemperatureMin || mixture.Temperature > ent.Comp.TemperatureMax)
            return false;

        var pressure = mixture.Pressure;
        if (pressure < ent.Comp.MinPressure || pressure > ent.Comp.MaxPressure)
            return false;

        // safe_air_limits: кислород 12–100 кПа, углекислота до 10.
        var oxygen = mixture.GetMoles(Content.Shared.Atmos.Gas.Oxygen) / mixture.TotalMoles * pressure;
        var co2 = mixture.GetMoles(Content.Shared.Atmos.Gas.CarbonDioxide) / mixture.TotalMoles * pressure;
        return oxygen >= 12 && oxygen <= 100 && co2 <= 10;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        var dt = _accumulator;
        _accumulator = 0;

        // Размножение и рост создают и удаляют рыб: сначала собираем список.
        var all = new List<Entity<ImperialFishComponent>>();
        var query = EntityQueryEnumerator<ImperialFishComponent>();
        while (query.MoveNext(out var queryUid, out var queryFish))
            all.Add((queryUid, queryFish));

        foreach (var ent in all)
        {
            var (uid, fish) = (ent.Owner, ent.Comp);
            if (TerminatingOrDeleted(uid))
                continue;

            if (fish.Dead)
            {
                _traits.ProcessDead(ent, dt);
                continue;
            }

            if (IsInStasis(ent))
                continue;

            DoFishProcess(ent, dt);
            if (fish.Dead || TerminatingOrDeleted(uid))
                continue;

            // На своём месте ловли рыба через время уплывает.
            if (!_container.IsEntityInContainer(uid) && _spots.GetSafeSpot(ent) is { } spot)
            {
                fish.TimeOnSafeSpot += dt;
                var threshold = GetStarvationMult(fish) > 0 ? StarvingSubmergingThreshold : SubmergingThreshold;
                if (fish.TimeOnSafeSpot >= threshold)
                {
                    _popup.PopupEntity(Loc.GetString("imperial-fish-disperses", ("fish", uid), ("spot", spot)), uid);
                    Released(ent, spot, null);
                }
            }
            else
            {
                fish.TimeOnSafeSpot = 0;
            }
        }
    }

    /// <summary>TRAIT_FISH_STASIS: аквариум в стазисе или стазисный мешок.</summary>
    public bool IsInStasis(Entity<ImperialFishComponent> ent)
    {
        if (!_container.TryGetContainingContainer(ent.Owner, out var container))
            return false;

        var ev = new ImperialFishStasisCheckEvent();
        RaiseLocalEvent(container.Owner, ref ev);
        return ev.Stasis;
    }

    /// <summary>do_fish_process().</summary>
    private void DoFishProcess(Entity<ImperialFishComponent> ent, float dt)
    {
        var growthStopped = false;
        if (_container.TryGetContainingContainer(ent.Owner, out var container))
        {
            var ev = new ImperialFishGrowthCheckEvent();
            RaiseLocalEvent(container.Owner, ref ev);
            growthStopped = ev.Stopped;
        }

        if (growthStopped)
        {
            ent.Comp.LastFeeding += TimeSpan.FromSeconds(dt);
            ent.Comp.BreedingWait += TimeSpan.FromSeconds(dt);
        }
        else
        {
            ProcessHealth(ent, dt);
            if (ent.Comp.Dead)
                return;

            var reproduce = new ImperialFishTryReproduceEvent(ent);
            if (container != null)
                RaiseLocalEvent(container.Owner, ref reproduce);
        }

        _traits.ProcessLife(ent, dt);

        var lifeEv = new ImperialFishLifeEvent(dt);
        RaiseLocalEvent(ent, ref lifeEv);
    }

    /// <summary>process_health().</summary>
    private void ProcessHealth(Entity<ImperialFishComponent> ent, float dt)
    {
        var change = 0f;
        if (!ProperEnvironment(ent))
            change -= 2.5f;

        var starvation = GetStarvationMult(ent.Comp);
        if (starvation > 0)
            change -= 0.25f * starvation;
        else
            change += 0.5f;

        if (_timing.CurTime < ent.Comp.TesliumUntil)
            change -= 0.65f;

        change *= dt;
        if (change < 0)
            DamageFish(ent, -change);
        else
            RepairFish(ent, change);
    }

    /// <summary>sate_hunger(): рост при кормлении.</summary>
    public void SateHunger(Entity<ImperialFishComponent> ent, bool growthStopped = false)
    {
        var comp = ent.Comp;
        if (growthStopped)
        {
            comp.LastFeeding = _timing.CurTime;
            return;
        }

        var hunger = GetHunger(comp);
        comp.LastFeeding = _timing.CurTime;
        if (hunger < 0.05f)
            return;

        float hungerMult;
        if (hunger <= ImperialFishing.GrowthPeak)
        {
            hungerMult = hunger / ImperialFishing.GrowthPeak;
        }
        else
        {
            hungerMult = 1 - (hunger - ImperialFishing.GrowthPeak) * 4;
            if (hungerMult <= 0)
                return;
        }

        var baseMult = ImperialFishing.GrowthMult;
        if (_timing.CurTime < comp.QuickGrowthUntil)
            baseMult *= 2.5f;

        var newSize = comp.Size;
        var newWeight = comp.Weight;
        var weightClass = GetWeightClass(comp.Size);
        if (comp.Size < comp.MaximumSize)
            newSize = MathF.Min(comp.Size + MathF.Ceiling((comp.MaximumSize - comp.Size) * baseMult / (weightClass * ImperialFishing.SizeWeightGrowthMalus) * hungerMult), comp.MaximumSize);
        if (comp.Weight < comp.MaximumWeight)
            newWeight = MathF.Min(comp.Weight + MathF.Ceiling((comp.MaximumWeight - comp.Weight) * baseMult / (ImperialFishing.GetWeightRank(comp.Weight) * ImperialFishing.SizeWeightGrowthMalus) * hungerMult), comp.MaximumWeight);

        if (newSize != comp.Size || newWeight != comp.Weight)
            UpdateSizeAndWeight(ent, newSize, newWeight);
    }

    /// <summary>w_class по размеру: 1 (крошечный) .. 6 (гигантский).</summary>
    public static int GetWeightClass(float size)
    {
        return size switch
        {
            <= ImperialFishing.SizeTinyMax => 1,
            <= ImperialFishing.SizeSmallMax => 2,
            <= ImperialFishing.SizeNormalMax => 3,
            <= ImperialFishing.SizeBulkyMax => 4,
            <= ImperialFishing.SizeHugeMax => 5,
            _ => 6,
        };
    }

    #endregion

    #region Взаимодействие

    /// <summary>released(): рыбу выпустили в место ловли.</summary>
    public void Released(Entity<ImperialFishComponent> ent, EntityUid location, EntityUid? user)
    {
        _audio.PlayPvs("/Audio/Effects/Fluids/splat.ogg", location);
        var ev = new ImperialFishReleasedEvent(ent, user);
        RaiseLocalEvent(location, ref ev);
        _spots.OnFishReleased(location, ent);
        QueueDel(ent);
    }

    private void OnExamined(Entity<ImperialFishComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;
        if (comp.CatcherName != null && comp.CatchDate != null)
            args.PushMarkup(Loc.GetString("imperial-fish-examine-caught", ("name", comp.CatcherName), ("date", comp.CatchDate)));

        if (_skill.CanExamineFish(args.Examiner))
        {
            args.PushMarkup(Loc.GetString("imperial-fish-examine-size", ("size", comp.Size)));
            args.PushMarkup(Loc.GetString("imperial-fish-examine-weight", ("weight", comp.Weight)));
            if (comp.Genegunned)
                args.PushMarkup(Loc.GetString("imperial-fish-examine-genegunned"));
        }

        if (GetHealthWarnings(ent, args.Examiner, false) is { } warnings)
            args.PushMarkup(warnings);

        if (comp.BaitQuality != ImperialBaitQuality.None)
            args.PushMarkup(Loc.GetString("imperial-fish-examine-bait"));

        if (comp.BitesAmount > 0)
            args.PushMarkup(Loc.GetString("imperial-fish-examine-bitten"));
    }

    /// <summary>get_health_warnings().</summary>
    public string? GetHealthWarnings(Entity<ImperialFishComponent> ent, EntityUid user, bool alwaysDeep)
    {
        if (!alwaysDeep && !_skill.CanExamineDeeper(user))
            return null;

        if (ent.Comp.Dead)
            return Loc.GetString("imperial-fish-examine-dead");

        var warnings = new List<string>();
        if (GetStarvationMult(ent.Comp) > 0)
            warnings.Add(Loc.GetString("imperial-fish-warning-starving"));
        if (!IsInStasis(ent) && !ProperEnvironment(ent))
            warnings.Add(Loc.GetString("imperial-fish-warning-drowning"));

        var health = GetHealthPercentage(ent.Comp);
        if (health <= 0.25f)
            warnings.Add(Loc.GetString("imperial-fish-warning-dying"));
        else if (health <= 0.5f)
            warnings.Add(Loc.GetString("imperial-fish-warning-very-unhealthy"));
        else if (health <= 0.75f)
            warnings.Add(Loc.GetString("imperial-fish-warning-unhealthy"));
        else if (health <= 0.9f)
            warnings.Add(Loc.GetString("imperial-fish-warning-mostly-healthy"));

        return warnings.Count == 0 ? null : Loc.GetString("imperial-fish-examine-warnings", ("warnings", string.Join(", ", warnings)));
    }

    /// <summary>try_pet_fish(): погладить рыбу в руке.</summary>
    private void OnUseInHand(Entity<ImperialFishComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryPet(ent, args.User, false);
    }

    public bool TryPet(Entity<ImperialFishComponent> ent, EntityUid user, bool inAquarium)
    {
        if (ent.Comp.Dead)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-pet-dead", ("fish", ent.Owner)), user, user);
            return false;
        }

        if (!ProperEnvironment(ent))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-pet-unwell", ("fish", ent.Owner)), user, user);
            return false;
        }

        if (_special.TryDolphishPet(ent, user, inAquarium))
            return true;

        if (_timing.CurTime < ent.Comp.PettedUntil)
        {
            _popup.PopupEntity(Loc.GetString(inAquarium ? "imperial-fish-pet-runs-away" : "imperial-fish-pet-squirms", ("fish", ent.Owner)), user, user);
            return false;
        }

        ent.Comp.PettedUntil = _timing.CurTime + TimeSpan.FromSeconds(30);
        Spawn("EffectHearts", Transform(ent).Coordinates);

        if (_traits.Has(ent.Comp, "Predator") && _random.Prob(0.5f))
        {
            _popup.PopupEntity(Loc.GetString(inAquarium ? "imperial-fish-pet-bite-aquarium" : "imperial-fish-pet-bite", ("fish", ent.Owner)), user, user, PopupType.SmallCaution);
            var damage = new DamageSpecifier(_proto.Index<Content.Shared.Damage.Prototypes.DamageTypePrototype>(new Robust.Shared.Prototypes.ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype>("Piercing")),
                FixedPoint2.New(GetWeightClass(ent.Comp.Size) * 2 + 1.2f));
            _damageable.TryChangeDamage(user, damage, origin: ent);
            _audio.PlayPvs("/Audio/Effects/bite.ogg", user);
            return true;
        }

        _popup.PopupEntity(Loc.GetString(inAquarium ? "imperial-fish-pet-dances" : "imperial-fish-pet", ("fish", ent.Owner)), user, user);
        _audio.PlayPvs(ent.Comp.PetSound, user);
        return true;
    }

    /// <summary>Разделка ножом (processable TOOL_KNIFE → fillet_type).</summary>
    private void OnInteractUsing(Entity<ImperialFishComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.FilletType == null || !_tool.HasQuality(args.Used, SlicingQuality))
            return;

        var amount = GetFilletAmount(ent.Comp);
        args.Handled = _tool.UseTool(args.Used, args.User, ent, 0.5f * amount, SlicingQuality, new ImperialFishFilletDoAfterEvent());
    }

    public static int GetFilletAmount(ImperialFishComponent comp)
    {
        return Math.Max((int) MathF.Round(comp.NumFillets * comp.Size / ImperialFishing.FilletNumberSizeDivisor), 1);
    }

    private void OnFilletDoAfter(Entity<ImperialFishComponent> ent, ref ImperialFishFilletDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.FilletType is not { } fillet)
            return;

        args.Handled = true;
        var coords = Transform(ent).Coordinates;
        var amount = GetFilletAmount(ent.Comp);
        for (var i = 0; i < amount; i++)
            Spawn(fillet, coords);

        var ev = new ImperialFishProcessedEvent(args.User);
        RaiseLocalEvent(ent, ref ev);
        QueueDel(ent);
    }

    private void OnGetMeleeDamage(Entity<ImperialFishComponent> ent, ref GetMeleeDamageEvent args)
    {
        args.Damage = GetForceDamage(ent);
    }

    /// <summary>update_fish_force(): урон рыбой зависит от веса и размера.</summary>
    public DamageSpecifier GetForceDamage(Entity<ImperialFishComponent> ent)
    {
        var weightClass = GetWeightClass(ent.Comp.Size);
        var force = 6f + weightClass switch
        {
            1 => -3,
            3 => 2,
            4 => 5,
            5 => 9,
            6 => 13,
            _ => 0,
        };

        var bonusMalus = ImperialFishing.GetWeightRank(ent.Comp.Weight) - weightClass;
        force = MathF.Round(force * (1 + bonusMalus * 0.1f), 1);
        force += _traits.GetForceBonus(ent, weightClass, bonusMalus);

        var type = _traits.HasElectrogenesis(ent.Comp) && !ent.Comp.Dead ? "Shock" : "Blunt";
        return new DamageSpecifier(_proto.Index<Content.Shared.Damage.Prototypes.DamageTypePrototype>(type), FixedPoint2.New(MathF.Max(force, 0)));
    }

    #endregion
}

/// <summary>Рыба умерла или ожила.</summary>
[ByRefEvent]
public readonly record struct ImperialFishStatusChangedEvent(bool Dead);

/// <summary>Контейнер рыбы (аквариум) решает, подходит ли среда.</summary>
[ByRefEvent]
public record struct ImperialFishEnvironmentCheckEvent(Entity<ImperialFishComponent> Fish, bool Handled = false, bool Proper = false);

/// <summary>Контейнер держит рыбу в стазисе.</summary>
[ByRefEvent]
public record struct ImperialFishStasisCheckEvent(bool Stasis = false);

/// <summary>Контейнер останавливает рост и размножение (TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH).</summary>
[ByRefEvent]
public record struct ImperialFishGrowthCheckEvent(bool Stopped = false);

/// <summary>Рыба готова размножиться — аквариум решает.</summary>
[ByRefEvent]
public readonly record struct ImperialFishTryReproduceEvent(Entity<ImperialFishComponent> Fish);

/// <summary>Рыбу выпустили в место ловли.</summary>
[ByRefEvent]
public readonly record struct ImperialFishReleasedEvent(Entity<ImperialFishComponent> Fish, EntityUid? User);

/// <summary>Рыбу разделали.</summary>
[ByRefEvent]
public readonly record struct ImperialFishProcessedEvent(EntityUid User);

/// <summary>Размер или вес рыбы изменились (COMSIG_FISH_UPDATE_SIZE_AND_WEIGHT).</summary>
[ByRefEvent]
public readonly record struct ImperialFishSizeUpdatedEvent;

/// <summary>Тик жизни живой рыбы (COMSIG_FISH_LIFE).</summary>
[ByRefEvent]
public readonly record struct ImperialFishLifeEvent(float FrameTime);

/// <summary>Рыба внутри контейнера (аквариума) умерла или ожила; поднимается на владельце контейнера.</summary>
[ByRefEvent]
public readonly record struct ImperialFishInContainerStatusChangedEvent(Entity<ImperialFishComponent> Fish, bool Dead);
