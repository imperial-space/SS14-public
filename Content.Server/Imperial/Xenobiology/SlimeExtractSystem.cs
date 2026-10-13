using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Prototypes;
using Content.Shared.Database;
using Content.Shared.Emp;
using Content.Shared.FixedPoint;
using Content.Shared.Flash;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Xenobiology;

/// <summary>
/// obj/item/slime_extract + chemical_reaction/slime: реакции реагентов в экстрактах слаймов.
/// </summary>
public sealed class SlimeExtractSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SlimeSystem _slime = default!;
    [Dependency] private readonly SlimeItemsSystem _items = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly SoundSpecifier AttackBlob = new SoundPathSpecifier("/Audio/Imperial/Xenobiology/attackblob.ogg");
    private static readonly SoundSpecifier PhaseIn = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");
    private static readonly EntProtoId FoamEffect = "Foam";
    private static readonly EntProtoId ClusterGrenade = "SlimeClusterGrenade";
    private static readonly EntProtoId VolatileClusterGrenade = "SlimeClusterGrenadeVolatile";
    private static readonly EntProtoId TimestopEffect = "SlimeTimestop";
    public static readonly ProtoId<WeightedRandomEntityPrototype> HostileMobs = "SlimeGoldHostileMobs";
    public static readonly ProtoId<WeightedRandomEntityPrototype> FriendlyMobs = "SlimeGoldFriendlyMobs";
    private static readonly string NeutralFaction = "PetsNT";

    /// <summary>Реагенты, которыми экстракт может активироваться сам (самоподдерживающийся, радужный).</summary>
    private static readonly HashSet<string> AutoActivateReagents = new() { "Plasma", "Water", "Blood", "Holywater", "Uranium", "Radium", "SlimeJelly" };

    private readonly List<(TimeSpan At, Action Act)> _scheduled = new();
    private List<EntProtoId>? _foods;
    private List<EntProtoId>? _drinks;
    private List<EntProtoId>? _crayons;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeExtractComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<SlimeExtractComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SlimeExtractComponent, Content.Shared.Examine.ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SlimeExtractComponent, GotEquippedHandEvent>(OnPickedUp);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => { _foods = null; _drinks = null; _crayons = null; });
    }

    private void OnPickedUp(Entity<SlimeExtractComponent> ent, ref GotEquippedHandEvent args)
    {
        ent.Comp.LastHolder = args.User;
    }

    private void OnExamined(Entity<SlimeExtractComponent> ent, ref Content.Shared.Examine.ExaminedEvent args)
    {
        if (ent.Comp.Uses > 1)
            args.PushMarkup(Loc.GetString("slime-extract-uses", ("uses", ent.Comp.Uses)));
    }

    #region Кроссбридинг

    /// <summary>interact_with_atom(): кормление взрослого слайма экстрактом мутирует его ядро.</summary>
    private void OnAfterInteract(Entity<SlimeExtractComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !TryComp<SlimeComponent>(target, out var slime))
            return;

        args.Handled = true;
        if (_mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("slime-extract-slime-dead"), target, args.User);
            return;
        }

        if (slime.Stage != SlimeLifeStage.Adult)
        {
            _popup.PopupEntity(Loc.GetString("slime-extract-not-adult"), target, args.User);
            return;
        }

        if (slime.CrossbreedModification != null && slime.CrossbreedModification != ent.Comp.CrossbreedModification)
        {
            _popup.PopupEntity(Loc.GetString("slime-extract-other-cross"), target, args.User);
            return;
        }

        slime.CrossbreedModification ??= ent.Comp.CrossbreedModification;
        slime.AppliedCrossbreedAmount++;
        Dirty(target, slime);
        QueueDel(ent);
        _popup.PopupEntity(Loc.GetString(slime.AppliedCrossbreedAmount == 1 ? "slime-extract-cross-start" : "slime-extract-cross-more",
            ("extract", ent.Owner)), target, args.User);
        _audio.PlayPvs(AttackBlob, target);

        if (slime.AppliedCrossbreedAmount >= SlimeConstants.ExtractCrossingRequired)
            _slime.SpawnCorecross((target, slime));
    }

    #endregion

    #region Реакции

    private void OnSolutionChanged(Entity<SlimeExtractComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (args.SolutionId != ent.Comp.SolutionName || ent.Comp.Uses <= 0 || TerminatingOrDeleted(ent))
            return;

        TryReact(ent);
    }

    /// <summary>Проверяет все реакции экстракта: REACTION_INSTANT, по одной за раз, пока есть заряды.</summary>
    public void TryReact(Entity<SlimeExtractComponent> ent)
    {
        for (var guard = 0; guard < 8 && ent.Comp.Uses > 0 && !TerminatingOrDeleted(ent); guard++)
        {
            if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out var solutionEnt, out var solution))
                return;

            SlimeReactionPrototype? chosen = null;
            var multiplier = FixedPoint2.Zero;
            foreach (var reaction in _proto.EnumeratePrototypes<SlimeReactionPrototype>())
            {
                if (reaction.Extract != ent.Comp.SlimeType)
                    continue;

                var mult = FixedPoint2.MaxValue;
                foreach (var (reagent, amount) in reaction.Reactants)
                {
                    var have = solution.GetTotalPrototypeQuantity(reagent);
                    if (have < amount)
                    {
                        mult = FixedPoint2.Zero;
                        break;
                    }

                    mult = FixedPoint2.Min(mult, have / amount);
                }

                if (mult <= FixedPoint2.Zero)
                    continue;
                chosen = reaction;
                multiplier = mult;
                break;
            }

            if (chosen == null)
                return;

            foreach (var (reagent, amount) in chosen.Reactants)
            {
                _solutions.RemoveReagent(solutionEnt.Value, reagent, amount * multiplier);
            }

            Activate(ent, chosen, multiplier.Float());
        }
    }

    /// <summary>GLOB.slime_extract_auto_activate_reactions: реакции, разрешённые для самоактивации.</summary>
    public List<SlimeReactionPrototype> AutoActivateReactions(ProtoId<SlimeTypePrototype> type)
    {
        return _proto.EnumeratePrototypes<SlimeReactionPrototype>()
            .Where(r => r.Extract == type && r.Reactants.Keys.All(k => AutoActivateReagents.Contains(k.Id)))
            .OrderBy(r => r.ID)
            .ToList();
    }

    /// <summary>auto_activate_reaction(): самоактивация реагентом из белого списка.</summary>
    public void AutoActivate(Entity<SlimeExtractComponent> ent, SlimeReactionPrototype? reaction = null)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (reaction == null)
        {
            var options = AutoActivateReactions(ent.Comp.SlimeType);
            if (options.Count == 0)
                return;
            reaction = _random.Pick(options);
        }

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out var solutionEnt, out _))
            return;
        foreach (var (reagent, amount) in reaction.Reactants)
        {
            _solutions.TryAddReagent(solutionEnt.Value, reagent, amount);
        }
    }

    /// <summary>on_reaction(): результат реакции, затем use_slime_core().</summary>
    private void Activate(Entity<SlimeExtractComponent> ent, SlimeReactionPrototype reaction, float createdVolume)
    {
        var coords = _transform.GetMoverCoordinates(ent);
        ent.Comp.Uses--;
        Dirty(ent);

        if (reaction.Dangerous)
            _adminLog.Add(LogType.ChemicalReaction, LogImpact.High,
                $"Опасная реакция слайма {reaction.ID} в {ToPrettyString(ent):extract}, последний держал {ToPrettyString(ent.Comp.LastHolder):user}");

        if (reaction.Message is { } message)
            _popup.PopupCoordinates(Loc.GetString(message), coords, PopupType.MediumCaution);

        // results
        if (reaction.Products.Count > 0 && _solutions.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out var solutionEnt, out _))
        {
            foreach (var (reagent, amount) in reaction.Products)
            {
                _solutions.TryAddReagent(solutionEnt.Value, reagent, amount * createdVolume);
            }
        }

        foreach (var (proto, count) in reaction.Spawn)
        {
            for (var i = 0; i < count; i++)
            {
                Spawn(proto, coords);
            }
        }

        foreach (var (proto, amount) in reaction.Stacks)
        {
            var stack = Spawn(proto, coords);
            if (TryComp<StackComponent>(stack, out var stackComp))
                _stack.SetCount((stack, stackComp), amount);
        }

        if (reaction.Delay > 0f)
        {
            var delay = TimeSpan.FromSeconds(reaction.Delay);
            Schedule(delay, () => DelayedEffect(ent, reaction, createdVolume));
            if (!reaction.DeletesExtract)
                ent.Comp.DeleteAt = _timing.CurTime + TimeSpan.FromSeconds(5.5);
            return;
        }

        Effect(ent, reaction, createdVolume, coords);

        // delete_extract(): экстракт без зарядов и без химических результатов исчезает.
        if (reaction.DeletesExtract && ent.Comp.Uses <= 0 && reaction.Products.Count == 0)
            QueueDel(ent);
    }

    private void DelayedEffect(Entity<SlimeExtractComponent> ent, SlimeReactionPrototype reaction, float createdVolume)
    {
        if (TerminatingOrDeleted(ent))
            return;
        Effect(ent, reaction, createdVolume, _transform.GetMoverCoordinates(ent));

        // use_slime_core() после отложенного эффекта.
        if (reaction.DeletesExtract && ent.Comp.Uses <= 0 && reaction.Products.Count == 0)
            QueueDel(ent);
    }

    private void Effect(Entity<SlimeExtractComponent> ent, SlimeReactionPrototype reaction, float createdVolume, EntityCoordinates coords)
    {
        var map = _transform.ToMapCoordinates(coords);
        switch (reaction.Effect)
        {
            case SlimeReactionEffect.SpawnSlime:
                var spawned = _slime.SpawnSlime(coords, reaction.Extract);
                _popup.PopupEntity(Loc.GetString("slime-reaction-spawn"), spawned, PopupType.MediumCaution);
                break;

            case SlimeReactionEffect.RandomSlimeOrBomb:
                if (createdVolume >= 5f)
                {
                    ArmGrenade(Spawn(ClusterGrenade, coords));
                    _popup.PopupCoordinates(Loc.GetString("slime-reaction-rainbow-bomb"), coords, PopupType.MediumCaution);
                }
                else
                {
                    var stage = _random.Prob(0.5f) ? SlimeLifeStage.Adult : SlimeLifeStage.Baby;
                    var random = _slime.SpawnSlime(coords, _slime.RandomSlimeType(), stage);
                    _popup.PopupEntity(Loc.GetString("slime-reaction-spawn"), random, PopupType.MediumCaution);
                }
                break;

            case SlimeReactionEffect.VolatileBomb:
                ArmGrenade(Spawn(VolatileClusterGrenade, coords));
                _popup.PopupCoordinates(Loc.GetString("slime-reaction-volatile-bomb"), coords, PopupType.LargeCaution);
                break;

            case SlimeReactionEffect.Foam:
                Foam(ent, coords);
                break;

            case SlimeReactionEffect.Freeze:
                SpawnGas(ent, Gas.Nitrogen, 50f, 2.7f);
                break;

            case SlimeReactionEffect.Fire:
                SpawnGas(ent, Gas.Plasma, 50f, 1000f);
                break;

            case SlimeReactionEffect.Emp:
                // empulse(turf, 3, 5): лёгкий радиус 5.
                _emp.EmpPulse(map, 5f, 50000f, TimeSpan.FromSeconds(60));
                break;

            case SlimeReactionEffect.Explosion:
                // explosion(1, 2, 4)
                _explosion.QueueExplosion(map, "Default", 140f, 10f, 30f, ent.Comp.LastHolder);
                break;

            case SlimeReactionEffect.Bloodlust:
                Bloodlust(coords);
                break;

            case SlimeReactionEffect.Timestop:
                Spawn(TimestopEffect, coords);
                ReturnToHolder(ent);
                break;

            case SlimeReactionEffect.MobSpawn:
                SpawnMobs(reaction, coords);
                break;

            case SlimeReactionEffect.RandomFood:
                Bork(coords, Foods());
                break;

            case SlimeReactionEffect.RandomDrinks:
                Bork(coords, Drinks());
                break;

            case SlimeReactionEffect.RandomCrayon:
                if (Crayons() is { Count: > 0 } crayons)
                    Spawn(_random.Pick(crayons), coords);
                break;
        }
    }

    /// <summary>Сепия: экстракт с зарядами возвращается в руку последнего, кто его держал.</summary>
    private void ReturnToHolder(Entity<SlimeExtractComponent> ent)
    {
        if (ent.Comp.Uses <= 0 || ent.Comp.LastHolder is not { } holder || TerminatingOrDeleted(holder))
            return;
        if (!_hands.TryPickupAnyHand(holder, ent))
            _transform.SetCoordinates(ent, _transform.GetMoverCoordinates(holder));
    }

    private void ArmGrenade(EntityUid grenade)
    {
        if (TryComp<SlimeGrenadeComponent>(grenade, out var comp))
            _items.Arm((grenade, comp), TimeSpan.FromSeconds(_random.NextFloat(1.5f, 6f)));
    }

    /// <summary>create_foam(): пена с остатком раствора экстракта.</summary>
    private void Foam(Entity<SlimeExtractComponent> ent, EntityCoordinates coords)
    {
        var solution = new Solution();
        if (_solutions.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out var solutionEnt, out var extractSolution))
        {
            solution = extractSolution.Clone();
            _solutions.RemoveAllSolution(solutionEnt.Value);
        }

        var foam = Spawn(FoamEffect, coords.SnapToGrid(EntityManager));
        _smoke.StartSmoke(foam, solution, 20f, 80);
        _popup.PopupCoordinates(Loc.GetString("slime-reaction-foam", ("extract", ent.Owner)), coords, PopupType.Medium);
    }

    /// <summary>atmos_spawn_air(): облако газа заданной температуры.</summary>
    private void SpawnGas(EntityUid source, Gas gas, float moles, float temperature)
    {
        var mixture = _atmos.GetTileMixture(source, excite: true);
        if (mixture == null)
            return;
        var add = new GasMixture(Atmospherics.CellVolume) { Temperature = temperature };
        add.AdjustMoles(gas, moles);
        _atmos.Merge(mixture, add);
    }

    /// <summary>slimebloodlust: дрессированные слаймы дичают, остальные звереют.</summary>
    private void Bloodlust(EntityCoordinates coords)
    {
        foreach (var (uid, slime) in _lookup.GetEntitiesInRange<SlimeComponent>(coords, 7f))
        {
            if (_mobState.IsDead(uid))
                continue;
            if (slime.HungerDisabled)
            {
                _popup.PopupEntity(Loc.GetString("slime-reaction-forgets", ("slime", uid)), uid, PopupType.MediumCaution);
                _slime.SetDefault((uid, slime));
                continue;
            }

            _slime.SetEnraged((uid, slime));
            _popup.PopupEntity(Loc.GetString("slime-reaction-frenzy", ("slime", uid)), uid, PopupType.MediumCaution);
        }
    }

    /// <summary>chemical_mob_spawn(): золотой экстракт призывает существ.</summary>
    private void SpawnMobs(SlimeReactionPrototype reaction, EntityCoordinates coords)
    {
        var pool = _proto.Index(reaction.HostileMobs ? HostileMobs : FriendlyMobs);
        for (var i = 0; i < reaction.MobCount; i++)
        {
            var proto = reaction.MobPrototype?.Id ?? pool.Pick(_random);
            var offset = new System.Numerics.Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f));
            var mob = Spawn(proto, coords.Offset(offset));
            if (reaction.NeutralMobs)
            {
                _faction.ClearFactions(mob, dirty: false);
                _faction.AddFaction(mob, NeutralFaction);
            }
        }
    }

    /// <summary>slimebork: вспышка и еда/напитки разлетаются вокруг.</summary>
    private void Bork(EntityCoordinates coords, List<EntProtoId> pool)
    {
        if (pool.Count == 0)
            return;

        _audio.PlayPvs(PhaseIn, coords);
        var flasher = Spawn(null, coords);
        _flash.FlashArea(flasher, null, 7f, TimeSpan.FromSeconds(2));
        QueueDel(flasher);

        var count = 4 + _random.Next(1, 3);
        for (var i = 0; i < count; i++)
        {
            var offset = _random.Prob(0.5f)
                ? new System.Numerics.Vector2(_random.Next(-3, 4), _random.Next(-3, 4)) * 0.5f
                : System.Numerics.Vector2.Zero;
            Spawn(_random.Pick(pool), coords.Offset(offset));
        }
    }

    public List<EntProtoId> Foods()
    {
        return _foods ??= _proto.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && !p.HideSpawnMenu && p.ID.StartsWith("Food") && p.HasComponent<EdibleComponent>(EntityManager.ComponentFactory))
            .Select(p => new EntProtoId(p.ID)).ToList();
    }

    public List<EntProtoId> Drinks()
    {
        return _drinks ??= _proto.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && !p.HideSpawnMenu && p.ID.StartsWith("Drink") && (p.ID.EndsWith("Can") || p.ID.EndsWith("BottleFull")))
            .Select(p => new EntProtoId(p.ID)).ToList();
    }

    private List<EntProtoId> Crayons()
    {
        return _crayons ??= _proto.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && p.ID.StartsWith("Crayon") && !p.ID.StartsWith("CrayonBox") && p.ID is not ("CrayonInfinite" or "CrayonBorg" or "CrayonInedible"))
            .Select(p => new EntProtoId(p.ID)).ToList();
    }

    #endregion

    private void Schedule(TimeSpan delay, Action act)
    {
        _scheduled.Add((_timing.CurTime + delay, act));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        for (var i = _scheduled.Count - 1; i >= 0; i--)
        {
            if (now < _scheduled[i].At)
                continue;
            var act = _scheduled[i].Act;
            _scheduled.RemoveAt(i);
            act();
        }

        var query = EntityQueryEnumerator<SlimeExtractComponent>();
        while (query.MoveNext(out var uid, out var extract))
        {
            if (extract.DeleteAt is not { } at || now < at)
                continue;
            extract.DeleteAt = null;
            if (extract.Uses <= 0)
                QueueDel(uid);
        }
    }
}

/// <summary>Сигнал гранате-кластеру слаймов взорваться.</summary>
public sealed class SlimeGrenadeDetonateEvent : EntityEventArgs;
