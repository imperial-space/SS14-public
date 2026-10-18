using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Cargo;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fishing;

/// <summary>Один рост рыбы (/datum/component/fish_growth).</summary>
public sealed class ImperialFishGrowth
{
    /// <summary>Прототип сущности или эволюция ("evolution:Id").</summary>
    public string Result = string.Empty;

    /// <summary>Прирост за секунду (100 / время роста).</summary>
    public float Rate;

    public float Maturation;
    public bool UseDropLoc = true;
    public bool DelOnGrow = true;
    public bool InheritName = true;

    public bool IsEvolution => Result.StartsWith(ImperialSpecialFishSystem.EvolutionPrefix);
}

/// <summary>Рост рыбы: в существо, в другой вид или откладывание икры. У одной рыбы может быть несколько ростов.</summary>
[RegisterComponent]
public sealed partial class ImperialFishGrowthComponent : Component
{
    [ViewVariables]
    public List<ImperialFishGrowth> Growths = new();
}

/// <summary>
/// Особые виды рыб: рост детёныша карпа, головастика, пропастного краба, жарешей и луна-рыб, икра;
/// исчезающие голо-рыбы и головастики; кристальная звезда; кусачий моржедельфин; вой бабелрыбы; цена рыбы для карго.
/// </summary>
public sealed class ImperialSpecialFishSystem : EntitySystem
{
    public const string EvolutionPrefix = "evolution:";
    private const string MoonfishEggs = "ImperialFoodMoonfishEggs";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ImperialFishSystem _fish = default!;
    [Dependency] private readonly ImperialFishTraitSystem _traits = default!;
    [Dependency] private readonly ImperialFishBreedingSystem _breeding = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;

    /// <summary>Мёртвые головастики исчезают через 12 секунд.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _vanish = new();

    /// <summary>Терпение моржедельфина и последнее предупреждение.</summary>
    private readonly Dictionary<EntityUid, (float Patience, int LastEffect)> _dolphish = new();

    private const float DolphishMaxPatience = 20;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialFishComponent, ImperialFishSizeUpdatedEvent>(OnSizeUpdated);
        SubscribeLocalEvent<ImperialFishComponent, ImperialFishLifeEvent>(OnLife);
        SubscribeLocalEvent<ImperialFishComponent, PriceCalculationEvent>(OnPrice);
    }

    private bool IsHolo(EntityUid uid) => Prototype(uid)?.ID.StartsWith("ImperialFishHolo") == true;

    /// <summary>Вызывается из ImperialFishSystem после инициализации рыбы.</summary>
    public void OnFishMapInit(Entity<ImperialFishComponent> ent)
    {
        // make_edible() пустой у головастика, голо-рыб, костерыбы и мастодонта.
        if (ent.Comp.FishId is "tadpole" || ImperialFishBreedingSystem.MadeOfBone(ent.Comp) || IsHolo(ent))
            RemComp<EdibleComponent>(ent);

        switch (ent.Comp.FishId)
        {
            case "tadpole":
                // 1% — редкая лягушка; в сборке одна лягушка.
                AddGrowth(ent, "MobFrog", _random.NextFloat(120, 180));
                break;
            case "moonfish":
                AddGrowth(ent, MoonfishEggs, 165, useDropLoc: false, delOnGrow: false, inheritName: false);
                break;
            case "dwarf_moonfish":
                AddGrowth(ent, MoonfishEggs, 255, useDropLoc: false, delOnGrow: false, inheritName: false);
                break;
        }
    }

    #region Рост

    /// <summary>update_size_and_weight() особых видов назначает время роста.</summary>
    private void OnSizeUpdated(Entity<ImperialFishComponent> ent, ref ImperialFishSizeUpdatedEvent args)
    {
        var fish = ent.Comp;
        switch (fish.FishId)
        {
            case "baby_carp":
            {
                var time = 4.5f * 60;
                time *= Math.Clamp(fish.Size / fish.AverageSize, 0.5f, 2);
                time *= Math.Clamp(fish.Weight / fish.AverageWeight, 0.5f, 2);
                AddGrowth(ent, "MobCarp", time);
                break;
            }
            case "chasm_crab":
            case "chasm_crab_ice":
            {
                var multiplier = 1f;
                if (fish.Size <= ImperialFishing.SizeTinyMax)
                    multiplier -= 0.2f;
                else if (fish.Size >= ImperialFishing.SizeBulkyMax)
                    multiplier += 0.8f;
                else if (fish.Size >= ImperialFishing.SizeNormalMax)
                    multiplier += 0.5f;
                else if (fish.Size >= ImperialFishing.SizeSmallMax)
                    multiplier += 0.2f;

                if (fish.Weight <= fish.AverageWeight - 200)
                    multiplier -= 0.1f * MathF.Round((fish.AverageWeight - fish.Weight) / 200);
                else if (fish.Weight >= fish.AverageWeight + 500)
                    multiplier += MathF.Min(0.1f * MathF.Round((fish.Weight - fish.AverageWeight) / 500), 2);

                // Лобстросити в сборке нет: пропастной краб дорастает до краба.
                AddGrowth(ent, "MobCrab", 600 * multiplier);
                break;
            }
            case "fryish":
                AddGrowth(ent, EvolutionPrefix + "Fritterish", 210 * (fish.Size / fish.AverageSize) * (fish.Weight / fish.AverageWeight), useDropLoc: false);
                break;
            case "fritterish":
                AddGrowth(ent, EvolutionPrefix + "Nessie", 480 * (fish.Size / fish.AverageSize) * (fish.Weight / fish.AverageWeight), useDropLoc: false);
                break;
            case "dwarf_moonfish":
            {
                var multiplier = fish.Size / (fish.AverageSize * 1.5f) * (fish.Weight / (fish.AverageWeight * 1.5f));
                AddGrowth(ent, EvolutionPrefix + "Moonfish", 150 * multiplier, useDropLoc: false);
                break;
            }
        }
    }

    /// <summary>AddComponent(/datum/component/fish_growth): тот же результат — только новая скорость (CheckDupeComponent).</summary>
    public void AddGrowth(EntityUid uid, string result, float seconds, bool useDropLoc = true, bool delOnGrow = true, bool inheritName = true)
    {
        var comp = EnsureComp<ImperialFishGrowthComponent>(uid);
        var rate = 100f / MathF.Max(seconds, 0.1f);
        if (comp.Growths.FirstOrDefault(g => g.Result == result) is { } existing)
        {
            existing.Rate = rate;
            return;
        }

        comp.Growths.Add(new ImperialFishGrowth
        {
            Result = result,
            Rate = rate,
            UseDropLoc = useDropLoc,
            DelOnGrow = delOnGrow,
            InheritName = inheritName,
        });
    }

    /// <summary>COMSIG_FISH_LIFE.</summary>
    private void OnLife(Entity<ImperialFishComponent> ent, ref ImperialFishLifeEvent args)
    {
        var fish = ent.Comp;
        if (fish.Dead)
            return;

        if (fish.FishId == "walro-dolphish")
            ProcessDolphish(ent, args.FrameTime);

        if (!TryComp<ImperialFishGrowthComponent>(ent, out var comp))
            return;

        foreach (var growth in comp.Growths.ToList())
        {
            if (fish.Dead || TerminatingOrDeleted(ent))
                return;

            var gain = growth.Rate * args.FrameTime;
            if (_timing.CurTime < fish.QuickGrowthUntil)
                gain *= 2;

            if (!GrowthChecks(ent, growth))
                continue;

            growth.Maturation += gain;
            if (growth.Maturation >= 100)
                FinishGrowing(ent, growth);
        }
    }

    /// <summary>COMSIG_FISH_BEFORE_GROWING: условия роста каждого вида.</summary>
    private bool GrowthChecks(Entity<ImperialFishComponent> ent, ImperialFishGrowth growth)
    {
        var fish = ent.Comp;
        var health = ImperialFishSystem.GetHealthPercentage(fish);
        EntityUid? aquarium = _container.TryGetContainingContainer(ent.Owner, out var container) && HasComp<ImperialAquariumComponent>(container.Owner)
            ? container.Owner
            : null;
        var stopped = aquarium is { } a && _breeding.GrowthStopped(a);

        // Яйца луна-рыбы: только в подходящей среде, сытой рыбой и не больше десяти икринок рядом.
        if (growth.Result == MoonfishEggs)
        {
            if (!_fish.ProperEnvironment(ent) || _fish.GetStarvationMult(fish) > 0)
                return false;

            var nearby = container != null
                ? container.ContainedEntities.ToList()
                : _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 0.4f).ToList();
            return nearby.Count(e => Prototype(e)?.ID == MoonfishEggs) <= 10;
        }

        if (growth.IsEvolution)
        {
            // fish_evolution/growth_checks: условия эволюции без аквариума (только среда).
            if (health < 0.5f || _fish.GetHunger(fish) >= 0.5f || stopped)
                return false;

            return _proto.TryIndex<ImperialFishEvolutionPrototype>(growth.Result[EvolutionPrefix.Length..], out var evolution)
                   && _breeding.CheckEvolution(evolution, ent, null, null);
        }

        switch (fish.FishId)
        {
            case "baby_carp":
            {
                var hunger = Math.Clamp((float) ((_timing.CurTime - fish.LastFeeding) / fish.FeedingFrequency), 0, 1);
                if (health <= 0.6f || hunger >= 0.6f)
                    return false;
                if (aquarium == null)
                    return true;
                return !stopped && _breeding.GetAquariumFishes(aquarium.Value).Count <= ImperialFishing.AquariumMaxBreedingPopulation * 0.5f;
            }
            case "tadpole":
                return _fish.GetHunger(fish) < 0.7f && !stopped;
            case "chasm_crab":
            case "chasm_crab_ice":
                if (health <= 0.6f || _fish.GetHunger(fish) >= 0.6f)
                    return false;
                if (aquarium == null)
                    return true;
                return !stopped && _breeding.GetAquariumFishes(aquarium.Value).Count <= ImperialFishing.AquariumMaxBreedingPopulation * 0.5f;
        }

        return true;
    }

    /// <summary>finish_growing().</summary>
    private void FinishGrowing(Entity<ImperialFishComponent> ent, ImperialFishGrowth growth)
    {
        var fish = ent.Comp;
        var inContainer = _container.TryGetContainingContainer(ent.Owner, out var container);
        EntityUid result;
        var dropped = false;

        if (growth.IsEvolution && _proto.TryIndex<ImperialFishEvolutionPrototype>(growth.Result[EvolutionPrefix.Length..], out var evolution))
        {
            result = _breeding.CreateOffspring(ent, evolution.NewFish, null, evolution);
            if (TryComp<ImperialFishComponent>(result, out var grown))
            {
                grown.BreedingWait = fish.BreedingWait;
                grown.LastFeeding = fish.LastFeeding;
                grown.Integrity = grown.MaxIntegrity * (fish.Integrity / fish.MaxIntegrity);
                Dirty(result, grown);
            }
        }
        else
        {
            // drop_location() аквариума — пол под ним; loc — сам аквариум.
            if (inContainer && !growth.UseDropLoc)
            {
                result = SpawnInContainerOrDrop(growth.Result, container!.Owner, container.ID);
            }
            else
            {
                result = Spawn(growth.Result, Transform(inContainer ? container!.Owner : ent.Owner).Coordinates);
                dropped = inContainer;
            }

            if (dropped)
            {
                _popup.PopupEntity(Loc.GetString("imperial-fish-jumps-out", ("result", result), ("container", container!.Owner)), result);
                _audio.PlayPvs("/Audio/Imperial/Fishing/fish_splash.ogg", result);
            }
        }

        if (!dropped)
        {
            var target = inContainer ? container!.Owner : ent.Owner;
            _popup.PopupEntity(Loc.GetString(growth.DelOnGrow ? "imperial-fish-grows-into" : "imperial-fish-lays",
                ("fish", ent.Owner), ("result", result)), target);
        }

        // TRAIT_WAS_RENAMED: имя отличается от имени прототипа.
        if (growth.InheritName && Prototype(ent) is { } proto && Name(ent) != proto.Name)
            _meta.SetEntityName(result, Name(ent));

        if (fish.FishId == "tadpole")
            _audio.PlayPvs("/Audio/Animals/frog_ribbit.ogg", result);

        if (growth.DelOnGrow)
            QueueDel(ent);
        else
            growth.Maturation = 0;
    }

    #endregion

    #region Особая жизнь и смерть

    /// <summary>
    /// Моржедельфин не любит, когда его держат: теряет терпение (медленнее в водяном паре и веселящем газе),
    /// на нуле кусает и вырывается. Вне рук терпение восстанавливается.
    /// </summary>
    private void ProcessDolphish(Entity<ImperialFishComponent> ent, float dt)
    {
        var (patience, lastEffect) = _dolphish.GetValueOrDefault(ent, (DolphishMaxPatience, 0));
        var reduction = 1f;
        if (_atmos.GetContainingMixture(ent.Owner, excite: false) is { } mixture)
        {
            if (mixture.GetMoles(Gas.WaterVapor) >= 5)
                reduction *= 0.5f;
            if (mixture.GetMoles(Gas.NitrousOxide) >= 5)
                reduction *= 0.25f;
        }

        if (!_container.TryGetContainingContainer(ent.Owner, out var container) || !HasComp<MobStateComponent>(container.Owner))
        {
            _dolphish[ent] = (Math.Clamp(patience + 1 / reduction * dt, 0, DolphishMaxPatience), lastEffect);
            return;
        }

        var holder = container.Owner;
        patience = Math.Clamp(patience - reduction * dt, 0, DolphishMaxPatience);
        if (patience <= 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-dolphish-bite", ("fish", ent.Owner), ("holder", holder)), holder, PopupType.MediumCaution);
            _damageable.TryChangeDamage(holder, _fish.GetForceDamage(ent), origin: ent);
            _hands.TryDrop(holder, ent.Owner);
            _audio.PlayPvs("/Audio/Effects/bite.ogg", holder);
            patience = DolphishMaxPatience;
        }
        else if (patience <= 10)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-dolphish-thrash", ("fish", ent.Owner), ("holder", holder)), holder, PopupType.SmallCaution);
        }
        else if (patience <= 15 && lastEffect != 1)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-dolphish-flinch", ("fish", ent.Owner), ("holder", holder)), holder);
            lastEffect = 1;
        }
        else if (patience > 15 && lastEffect != 2)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-dolphish-uncomfy", ("fish", ent.Owner), ("holder", holder)), holder);
            lastEffect = 2;
        }

        _dolphish[ent] = (patience, lastEffect);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var uid in _dolphish.Keys.ToList())
        {
            if (TerminatingOrDeleted(uid))
                _dolphish.Remove(uid);
        }

        if (_vanish.Count == 0)
            return;

        foreach (var (uid, at) in _vanish.ToList())
        {
            if (_timing.CurTime < at)
                continue;

            _vanish.Remove(uid);
            if (!TerminatingOrDeleted(uid) && CompOrNull<ImperialFishComponent>(uid)?.Dead == true)
                QueueDel(uid);
        }
    }

    /// <summary>set_status() особых видов.</summary>
    /// <summary>Вызывается из ImperialFishSystem.SetDead.</summary>
    public void OnFishStatusChanged(Entity<ImperialFishComponent> ent, bool dead)
    {
        if (!dead)
        {
            _vanish.Remove(ent);
            return;
        }

        // Голограммы исчезают.
        if (IsHolo(ent))
        {
            QueueDel(ent);
            return;
        }

        switch (ent.Comp.FishId)
        {
            case "tadpole":
                _vanish[ent] = _timing.CurTime + TimeSpan.FromSeconds(12);
                break;
            case "chrystarfish":
                if (ent.Comp.FilletType is { } fillet)
                    Spawn(fillet, Transform(ent).Coordinates);
                _audio.PlayPvs("/Audio/Effects/glass_break1.ogg", ent);
                QueueDel(ent);
                break;
            case "babbelfish":
                PsyWail(ent);
                break;
        }
    }

    /// <summary>psy_wail(): вой бабелрыбы при смерти валит с ног всех рядом и убивает рыб в радиусе 7 тайлов.</summary>
    private void PsyWail(Entity<ImperialFishComponent> ent)
    {
        _popup.PopupEntity(Loc.GetString("imperial-fish-babbelfish-wail", ("fish", ent.Owner)), ent, PopupType.LargeCaution);
        _audio.PlayPvs("/Audio/Imperial/Fishing/fish_psyblast.ogg", ent);
        var coords = Transform(ent).Coordinates;
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 7))
        {
            _stun.TryKnockdown(mob.Owner, TimeSpan.FromSeconds(1.5), true);
        }

        var affected = 0;
        foreach (var other in _lookup.GetEntitiesInRange<ImperialFishComponent>(coords, 7))
        {
            // range(7) в TG не видит рыб в аквариумах и рюкзаках.
            if (other.Owner == ent.Owner || other.Comp.Dead || _traits.Has(other.Comp, "PsychicAura") || _container.IsEntityInContainer(other))
                continue;

            _fish.SetDead(other, true);
            affected++;
        }

        if (affected > 0)
            _popup.PopupEntity(Loc.GetString("imperial-fish-babbelfish-kills", ("fish", ent.Owner), ("count", affected)), ent, PopupType.LargeCaution);
    }

    /// <summary>Моржедельфин кусает вместо того, чтобы дать себя погладить.</summary>
    public bool TryDolphishPet(Entity<ImperialFishComponent> ent, EntityUid user, bool inAquarium)
    {
        if (ent.Comp.FishId != "walro-dolphish")
            return false;

        _popup.PopupEntity(Loc.GetString("imperial-fish-dolphish-pet", ("fish", ent.Owner)), user, user, PopupType.MediumCaution);
        _damageable.TryChangeDamage(user, _fish.GetForceDamage(ent), origin: ent);
        _audio.PlayPvs("/Audio/Effects/bite.ogg", user);
        if (!inAquarium)
            _hands.TryDrop(user, ent.Owner);
        return true;
    }

    #endregion

    /// <summary>get_export_price(): 30 + (размер × вес × 0.01)^0.85, мягкий потолок 6000.</summary>
    private void OnPrice(Entity<ImperialFishComponent> ent, ref PriceCalculationEvent args)
    {
        var fish = ent.Comp;
        double price;
        if (fish.FishId == "tadpole")
        {
            price = 2;
        }
        else
        {
            price = 30 + Math.Pow(fish.Size * fish.Weight * 0.01, 0.85);
            if (price > 6000)
                price = 6000 + Math.Pow(price - 6000, 0.86);
            if (fish.FishId == "mastodon")
                price *= 1.2;
            if (fish.LowPrice)
                price *= 0.05;
        }

        // get_export_price() — вся цена рыбы, без реагентов и материалов.
        args.Price = price;
        args.Handled = true;
    }
}
