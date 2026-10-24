using System.Linq;
using Content.Server.Electrocution;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Popups;
using Content.Shared.Slippery;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Жизнь черт рыб: apply_to_fish, COMSIG_FISH_LIFE, яд жала, чернила, электрогенез и т. п.
/// </summary>
public sealed partial class ImperialFishTraitSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    /// <summary>ELECTROGENESIS_DURATION.</summary>
    private static readonly TimeSpan ElectrogenesisCooldown = TimeSpan.FromSeconds(40);

    /// <summary>Конец перезарядки чернил по рыбам (TRAIT_FISH_INK_ON_COOLDOWN).</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _inkCooldown = new();

    /// <summary>Хищники, отравившиеся токсичной рыбой.</summary>
    private readonly HashSet<EntityUid> _poisoned = new();

    private void InitializeLife()
    {
        SubscribeLocalEvent<ImperialFishComponent, MeleeHitEvent>(OnFishMeleeHit);
        SubscribeLocalEvent<ImperialFishComponent, ImperialFishStatusChangedEvent>(OnStatusChanged);
        SubscribeLocalEvent<ImperialFishComponent, ImperialFishProcessedEvent>(OnProcessed);
        SubscribeLocalEvent<ImperialFishComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<ImperialFishComponent> ent, ref ComponentShutdown args)
    {
        _inkCooldown.Remove(ent);
        _poisoned.Remove(ent);
    }

    public bool Has(ImperialFishComponent fish, string trait) => fish.Traits.Any(t => t.Id == trait);

    /// <summary>TRAIT_FISH_ELECTROGENESIS: черта или кормление теслием.</summary>
    public bool HasElectrogenesis(ImperialFishComponent fish) => Has(fish, "Electrogenesis") || _timing.CurTime < fish.TesliumUntil;

    private ImperialFishSystem FishSystem => EntityManager.System<ImperialFishSystem>();

    /// <summary>apply_to_fish() для всех черт рыбы.</summary>
    public void ApplyAll(Entity<ImperialFishComponent> ent)
    {
        foreach (var trait in ent.Comp.Traits.ToList())
            Apply(ent, trait, true);
    }

    public bool Apply(Entity<ImperialFishComponent> ent, string trait, bool initial)
    {
        if (!_proto.TryIndex<ImperialFishTraitPrototype>(trait, out var proto))
            return false;

        if (!initial)
        {
            if (Has(ent.Comp, trait))
                return false;
            if (proto.Whitelist != null && MetaData(ent).EntityPrototype is { } fishProto && !proto.Whitelist.Contains(fishProto.ID))
                return false;
            if (ent.Comp.Traits.Any(t => proto.Incompatible.Contains(t)))
                return false;

            ent.Comp.Traits.Add(trait);
        }

        switch (trait)
        {
            case "Lubed":
                EnsureComp<SlipperyComponent>(ent);
                break;
            case "Amphibious":
                if (ent.Comp.RequiredFluid == ImperialFishFluid.Air)
                    ent.Comp.RequiredFluid = ImperialFishFluid.Freshwater;
                break;
        }

        Dirty(ent);
        return true;
    }

    public void Remove(Entity<ImperialFishComponent> ent, string trait)
    {
        if (!ent.Comp.Traits.Remove(trait))
            return;

        switch (trait)
        {
            case "Lubed":
                RemComp<SlipperyComponent>(ent);
                break;
            case "Amphibious":
                if (MetaData(ent).EntityPrototype is { } proto && _data.GetFish(proto.ID) is { } data)
                    ent.Comp.RequiredFluid = data.RequiredFluid;
                break;
        }

        Dirty(ent);
    }

    /// <summary>COMSIG_GENERATE_REAGENTS_TO_ADD: реагенты черт в съедобной рыбе.</summary>
    public void AddTraitReagents(Entity<ImperialFishComponent> ent, Dictionary<string, float> reagents)
    {
        foreach (var trait in ent.Comp.Traits)
        {
            if (!_proto.TryIndex(trait, out var proto))
                continue;

            // Галлюциноген готовкой разрушается.
            if (trait == "Hallucinogenic" && ent.Comp.Cooked)
                continue;

            foreach (var (reagent, amount) in proto.Reagents)
                reagents[reagent] = reagents.GetValueOrDefault(reagent) + amount;

            if (trait == "Electrogenesis")
            {
                reagents.Remove("Blood");
            }
        }
    }

    /// <summary>Бонус к урону от черт (COMSIG_FISH_FORCE_UPDATED).</summary>
    public float GetForceBonus(Entity<ImperialFishComponent> ent, int weightClass, int bonusMalus)
    {
        var bonus = 0f;
        if (Has(ent.Comp, "Stinger"))
            bonus += 1 + weightClass + bonusMalus;
        if (HasElectrogenesis(ent.Comp) && !ent.Comp.Dead)
            bonus += 10 - weightClass;
        return bonus;
    }

    /// <summary>Рыбы в том же аквариуме (get_aquarium_fishes).</summary>
    public List<Entity<ImperialFishComponent>> GetNeighbours(EntityUid fish, bool aquariumOnly = true)
    {
        var result = new List<Entity<ImperialFishComponent>>();
        if (!_container.TryGetContainingContainer(fish, out var container))
            return result;

        if (aquariumOnly && !HasComp<ImperialAquariumComponent>(container.Owner))
            return result;

        foreach (var other in container.ContainedEntities)
        {
            if (other != fish && TryComp<ImperialFishComponent>(other, out var comp))
                result.Add((other, comp));
        }

        return result;
    }

    /// <summary>COMSIG_FISH_LIFE.</summary>
    public void ProcessLife(Entity<ImperialFishComponent> ent, float dt)
    {
        var fish = ent.Comp;
        var fishSys = FishSystem;

        if (_poisoned.Contains(ent))
            fishSys.DamageFish(ent, 3 * dt);

        if (!Has(fish, "Electrogenesis") && _timing.CurTime < fish.TesliumUntil)
            TryElectrogenesis(ent);

        foreach (var trait in fish.Traits.Select(t => t.Id).ToList())
        {
            if (fish.Dead || TerminatingOrDeleted(ent))
                return;

            switch (trait)
            {
                case "Nocturnal":
                    if (IsLit(Transform(ent).Coordinates))
                        fishSys.DamageFish(ent, 0.5f * dt);
                    break;

                case "Emulsijack":
                case "PsychicAura":
                {
                    var resist = trait == "Emulsijack" ? new[] { "Emulsijack", "ToxinImmunity" } : new[] { "PsychicAura" };
                    var emulsified = false;
                    foreach (var victim in GetNeighbours(ent))
                    {
                        if (resist.Any(r => Has(victim.Comp, r)))
                            continue;

                        fishSys.DamageFish(victim, 3 * dt);
                        emulsified = true;
                    }

                    if (emulsified)
                    {
                        fishSys.RepairFish(ent, 3 * dt);
                        fishSys.SateHunger(ent);
                    }

                    break;
                }

                case "Necrophage":
                    if (fishSys.GetHunger(fish) < 0.75f)
                        break;

                    foreach (var victim in GetNeighbours(ent))
                    {
                        if (!victim.Comp.Dead || Has(victim.Comp, "Yucky"))
                            continue;

                        EatFish(ent, victim);
                        break;
                    }

                    break;

                case "Predator":
                    if (fishSys.GetHunger(fish) < 0.75f)
                        break;

                    foreach (var victim in GetNeighbours(ent).OrderBy(_ => _random.Next()))
                    {
                        if (victim.Comp.Size >= fish.Size * 0.7f || victim.Comp.Dead || Has(victim.Comp, "Yucky") || _random.Prob(1 - MathF.Pow(0.2f, dt)))
                            continue;

                        EatFish(ent, victim);
                        break;
                    }

                    break;

                case "Territorial":
                {
                    if (!_random.Prob(1 - MathF.Pow(0.99f, dt)))
                        break;

                    var others = GetNeighbours(ent);
                    if (others.Count < 5)
                        break;

                    foreach (var victim in others.OrderBy(_ => _random.Next()))
                    {
                        if (victim.Comp.Dead)
                            continue;

                        _popup.PopupEntity(Loc.GetString("imperial-fish-territorial-attack", ("fish", ent.Owner), ("victim", victim.Owner)), ent, PopupType.SmallCaution);
                        fishSys.DamageFish(victim, MathF.Round(_random.Next(4, 21) * (fish.Size / MathF.Max(victim.Comp.Size, 1))));
                        break;
                    }

                    break;
                }

                case "Anxiety":
                    if (GetNeighbours(ent, aquariumOnly: false).Count >= 3)
                    {
                        _popup.PopupEntity(Loc.GetString("imperial-fish-anxiety-death", ("fish", ent.Owner)), ent, PopupType.SmallCaution);
                        fishSys.SetDead(ent, true, silent: true);
                    }

                    break;

                case "Electrogenesis":
                    TryElectrogenesis(ent);
                    break;
            }
        }
    }

    /// <summary>Жизнь мёртвой рыбы: самовоскрешение.</summary>
    public void ProcessDead(Entity<ImperialFishComponent> ent, float dt)
    {
        if (ent.Comp.ReviveAt is not { } at || _timing.CurTime < at)
            return;

        ent.Comp.ReviveAt = null;
        if (!Has(ent.Comp, "Revival"))
            return;

        FishSystem.SetDead(ent, false);
        var target = _container.TryGetContainingContainer(ent.Owner, out var container) ? container.Owner : ent.Owner;
        _popup.PopupEntity(Loc.GetString("imperial-fish-revived", ("fish", ent.Owner)), target);
    }

    private void OnStatusChanged(Entity<ImperialFishComponent> ent, ref ImperialFishStatusChangedEvent args)
    {
        if (args.Dead)
        {
            _poisoned.Remove(ent);
            if (Has(ent.Comp, "Revival"))
                ent.Comp.ReviveAt = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(60, 121));
        }
    }

    /// <summary>eat_fish(): хищник съедает добычу.</summary>
    private void EatFish(Entity<ImperialFishComponent> predator, Entity<ImperialFishComponent> prey)
    {
        var message = prey.Comp.Dead ? "imperial-fish-eats-carcass" : "imperial-fish-hunts";
        var target = _container.TryGetContainingContainer(predator.Owner, out var container) ? container.Owner : predator.Owner;
        _popup.PopupEntity(Loc.GetString(message, ("fish", predator.Owner), ("prey", prey.Owner)), target, PopupType.SmallCaution);

        if ((Has(prey.Comp, "Toxic") || Has(prey.Comp, "Carpotoxin")) && !Has(predator.Comp, "ToxinImmunity"))
            _poisoned.Add(predator);

        QueueDel(prey);
        FishSystem.SateHunger(predator);
    }

    /// <summary>try_electrogenesis(): разряд по соседям, в биогенераторе — энергия.</summary>
    private void TryElectrogenesis(Entity<ImperialFishComponent> ent)
    {
        if (_timing.CurTime < ent.Comp.NextElectrogenesis || FishSystem.GetStarvationMult(ent.Comp) > 0)
            return;

        ent.Comp.NextElectrogenesis = _timing.CurTime + ElectrogenesisCooldown + TimeSpan.FromSeconds(_random.Next(-10, 11));

        var ev = new ImperialFishElectrogenesisEvent(ent, ent.Comp.ElectrogenesisPower * ent.Comp.Size * 0.1f);
        if (_container.TryGetContainingContainer(ent.Owner, out var container))
            RaiseLocalEvent(container.Owner, ref ev);

        if (ev.Handled)
            return;

        // 1 кДж, около 5 урона: «ай!» всем рядом.
        foreach (var mob in _lookup.GetEntitiesInRange<Content.Shared.Mobs.Components.MobStateComponent>(Transform(ent).Coordinates, 1.5f))
            _electrocution.TryDoElectrocution(mob, ent, 5, TimeSpan.FromSeconds(1), true);
    }

    /// <summary>Яд жала и чернила при ударе рыбой.</summary>
    private void OnFishMeleeHit(Entity<ImperialFishComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var fish = ent.Comp;
        if (Has(fish, "Stinger"))
        {
            foreach (var target in args.HitEntities)
                InjectVenom(ent, target);
        }

        if (Has(fish, "Ink") && !fish.Dead && (!_inkCooldown.TryGetValue(ent, out var until) || _timing.CurTime >= until))
        {
            var target = args.HitEntities[0];
            _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(target, "TemporaryBlindness", TimeSpan.FromSeconds(_random.Next(4, 9)), true);
            _popup.PopupEntity(Loc.GetString("imperial-fish-inked", ("target", target), ("fish", ent.Owner)), target, PopupType.MediumCaution);
            _audio.PlayPvs("/Audio/Effects/splat.ogg", target);
            _inkCooldown[ent] = _timing.CurTime + TimeSpan.FromSeconds(9);
        }
    }

    /// <summary>venomous: токсичная рыба, ядовитые шипы и галлюциноген впрыскиваются жалом.</summary>
    private void InjectVenom(Entity<ImperialFishComponent> ent, EntityUid target)
    {
        var fish = ent.Comp;
        var venoms = new List<(string Reagent, float Live, float Dead)>();
        if (Has(fish, "Toxic"))
            venoms.Add(("Toxin", 0.25f, 0.1f));
        if (Has(fish, "Carpotoxin"))
            venoms.Add(("CarpoToxin", 0.25f * 6, 0.1f * 6));
        if (Has(fish, "ToxicBarbs"))
            venoms.Add(("Toxin", 0.7f, 0.3f));
        if (Has(fish, "Hallucinogenic"))
            venoms.Add(("MindbreakerToxin", 0.7f, 0.3f));

        if (venoms.Count == 0 || !_solution.TryGetInjectableSolution(target, out var soln, out _))
            return;

        foreach (var (reagent, live, dead) in venoms)
        {
            var mult = fish.Dead ? dead : live;
            var amount = MathF.Max(MathF.Round(fish.Weight / ImperialFishing.GrindResultsWeightDivisor * mult, 1), mult);
            _solution.TryAddReagent(soln.Value, reagent, FixedPoint2.New(amount));
        }
    }

    /// <summary>Разделка рыбы с чернилами даёт чернильный мешок.</summary>
    private void OnProcessed(Entity<ImperialFishComponent> ent, ref ImperialFishProcessedEvent args)
    {
        if (Has(ent.Comp, "Ink") && _proto.HasIndex<EntityPrototype>(new Robust.Shared.Prototypes.ProtoId<EntityPrototype>("ImperialFoodInkSac")))
            Spawn("ImperialFoodInkSac", Transform(ent).Coordinates);
    }
}

/// <summary>Рыба с электрогенезом даёт разряд. Аквариум-генератор забирает энергию и ставит Handled.</summary>
[ByRefEvent]
public record struct ImperialFishElectrogenesisEvent(Entity<ImperialFishComponent> Fish, float Power, bool Handled = false);
