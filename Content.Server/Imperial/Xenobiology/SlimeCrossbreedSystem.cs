using Content.Shared.Prototypes;
using Content.Shared.Coordinates.Helpers;
using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Server.Electrocution;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Server.Polymorph.Systems;
using Content.Server.Temperature.Systems;
using Content.Shared.Atmos;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Electrocution;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Slippery;
using Content.Shared.Stacks;
using Content.Shared.Stunnable;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Xenobiology;

/// <summary>
/// Кроссбридинг слаймов (code/modules/research/xenobiology/crossbreeding).
/// </summary>
public sealed partial class SlimeCrossbreedSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SlimeExtractSystem _extract = default!;
    [Dependency] private readonly SlimeItemsSystem _items = default!;
    [Dependency] private readonly SlimeSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SlimeSystem _slime = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly SoundSpecifier Bubbles = new SoundPathSpecifier("/Audio/Imperial/Xenobiology/bubbles.ogg");
    private static readonly SoundSpecifier AttackBlob = new SoundPathSpecifier("/Audio/Imperial/Xenobiology/attackblob.ogg");
    private static readonly SoundSpecifier Splat = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");
    private static readonly SoundSpecifier EatFood = new SoundCollectionSpecifier("eating");
    private static readonly SoundSpecifier Sparks = new SoundCollectionSpecifier("sparks");
    private static readonly ProtoId<ReagentPrototype> Plasma = "Plasma";
    private static readonly EntProtoId SmokeProto = "Smoke";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeCrossbreedComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SlimeCrossActivatedComponent, UseInHandEvent>(OnActivatedUse);
        SubscribeLocalEvent<SlimeCrossLinkComponent, AfterInteractEvent>(OnLinkInteract);

        InitializePassive();
        InitializeStabilized();
        InitializeEffects();
        InitializeItems();
    }

    private void OnExamined(Entity<SlimeCrossbreedComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.EffectDesc is { } desc)
            args.PushMarkup(Loc.GetString(desc));
    }

    #region Хелперы

    private string Colour(EntityUid uid)
    {
        return TryComp<SlimeCrossbreedComponent>(uid, out var cross) ? cross.SlimeType.Id : string.Empty;
    }

    private EntityCoordinates At(EntityUid uid)
    {
        return _transform.GetMoverCoordinates(uid);
    }

    private void Popup(string key, EntityUid at, params (string, object)[] args)
    {
        _popup.PopupEntity(Loc.GetString(key, args), at, PopupType.Medium);
    }

    private void PopupUser(string key, EntityUid user, params (string, object)[] args)
    {
        _popup.PopupEntity(Loc.GetString(key, args), user, user);
    }

    /// <summary>Появляется в руке, иначе на полу.</summary>
    private EntityUid SpawnForUser(EntProtoId proto, EntityUid user)
    {
        var item = Spawn(proto, At(user));
        _hands.TryPickupAnyHand(user, item);
        return item;
    }

    private void SpawnStack(EntProtoId proto, EntityCoordinates coords, int count)
    {
        var stack = Spawn(proto, coords);
        if (TryComp<StackComponent>(stack, out var stackComp))
            _stack.SetCount((stack, stackComp), count);
    }

    private EntityCoordinates Scatter(EntityCoordinates coords, int maxSteps = 3)
    {
        if (!_random.Prob(0.5f))
            return coords;
        return coords.Offset(_random.NextAngle().ToVec() * _random.Next(1, maxSteps + 1) * 0.5f);
    }

    /// <summary>Вводит реагент существу (reagents.add_reagent).</summary>
    private void InjectReagent(EntityUid uid, string reagent, float amount)
    {
        var solution = new Solution();
        solution.AddReagent(reagent, FixedPoint2.New(amount));
        _bloodstream.TryAddToBloodstream(uid, solution);
    }

    private void Damage(EntityUid uid, string type, float amount, EntityUid? origin = null)
    {
        _damageable.TryChangeDamage(uid, new DamageSpecifier(_proto.Index<Content.Shared.Damage.Prototypes.DamageTypePrototype>(type), amount),
            ignoreResistances: true, origin: origin);
    }

    /// <summary>Лечит по группам урона.</summary>
    private void Heal(EntityUid uid, float brute, float burn, float toxin, float airloss = 0f)
    {
        var spec = new DamageSpecifier();
        void Group(string group, float amount)
        {
            if (amount <= 0f || !_proto.TryIndex<Content.Shared.Damage.Prototypes.DamageGroupPrototype>(group, out var proto))
                return;
            spec += new DamageSpecifier(proto, -amount);
        }

        Group("Brute", brute);
        Group("Burn", burn);
        Group("Toxin", toxin);
        Group("Airloss", airloss);
        _damageable.TryChangeDamage(uid, spec, ignoreResistances: true, interruptsDoAfters: false);
    }

    /// <summary>do_chem_smoke().</summary>
    private void ChemSmoke(EntityCoordinates coords, string reagent, float amount, int range)
    {
        var solution = new Solution();
        solution.AddReagent(reagent, FixedPoint2.New(amount));
        var smoke = Spawn(SmokeProto, coords.SnapToGrid(EntityManager, null));
        _smoke.StartSmoke(smoke, solution, 15f, range * range);
    }

    /// <summary>atmos_spawn_air().</summary>
    private void SpawnGas(EntityUid source, Gas gas, float moles, float? temperature = null)
    {
        var mixture = _atmos.GetTileMixture(source, excite: true);
        if (mixture == null)
            return;
        var add = new GasMixture(Atmospherics.CellVolume) { Temperature = temperature ?? mixture.Temperature };
        add.AdjustMoles(gas, moles);
        _atmos.Merge(mixture, add);
    }

    /// <summary>new /obj/effect/hotspot: тайл вспыхивает, существа на нём загораются.</summary>
    private void Hotspot(EntityCoordinates coords)
    {
        var xform = Transform(coords.EntityId);
        if (xform.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var gridComp))
        {
            var tile = _map.TileIndicesFor(grid, gridComp, coords);
            _atmos.HotspotExpose(grid, tile, 700f, 50f);
        }

        foreach (var mob in _lookup.GetEntitiesInRange<Content.Shared.Atmos.Components.FlammableComponent>(coords, 0.5f))
        {
            _flammable.AdjustFireStacks(mob, 1f, mob.Comp, ignite: true);
        }
    }

    /// <summary>Свободные клетки вокруг (range/orange).</summary>
    private IEnumerable<EntityCoordinates> TilesAround(EntityCoordinates center, int range, int minDistance = 0)
    {
        var map = _transform.ToMapCoordinates(center);
        for (var x = -range; x <= range; x++)
        {
            for (var y = -range; y <= range; y++)
            {
                if (Math.Max(Math.Abs(x), Math.Abs(y)) < minDistance)
                    continue;
                var mapPos = new MapCoordinates(map.Position + new Vector2(x, y), map.MapId);
                yield return _transform.ToCoordinates(mapPos);
            }
        }
    }

    /// <summary>create_random_mob(): существо из золотого пула.</summary>
    private EntityUid SpawnGoldMob(EntityCoordinates coords, bool hostile)
    {
        var pool = _proto.Index(hostile ? SlimeExtractSystem.HostileMobs : SlimeExtractSystem.FriendlyMobs);
        return Spawn(pool.Pick(_random), coords);
    }

    /// <summary>add_ally(user): существо дружит с фракциями пользователя.</summary>
    private void MakeAlly(EntityUid mob, EntityUid user)
    {
        if (!TryComp<Content.Shared.NPC.Components.NpcFactionMemberComponent>(user, out var userFactions))
            return;
        _faction.ClearFactions(mob, dirty: false);
        _faction.AddFactions(mob, userFactions.Factions);
    }

    #endregion

    #region Активация

    /// <summary>attack_self(): нужно 10u плазмы внутри.</summary>
    private void OnActivatedUse(Entity<SlimeCrossActivatedComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Active)
            return;
        args.Handled = true;

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solutionEnt, out var solution)
            || solution.GetTotalPrototypeQuantity(Plasma) < ent.Comp.PlasmaRequired)
        {
            PopupUser("slime-cross-need-plasma", args.User);
            return;
        }

        _solutions.RemoveReagent(solutionEnt.Value, Plasma, ent.Comp.PlasmaRequired);
        PopupUser("slime-cross-squeeze", args.User);
        _audio.PlayPvs(Bubbles, ent);

        if (!TryComp<SlimeCrossbreedComponent>(ent, out var cross))
            return;

        var consumed = cross.Effect switch
        {
            "burning" => Burning(ent, Colour(ent), args.User),
            "charged" => Charged(ent, Colour(ent), args.User),
            "chilling" => Chilling(ent, Colour(ent), args.User),
            _ => true,
        };

        if (consumed)
            QueueDel(ent);
    }

    /// <summary>chilling/bluespace и chilling/sepia: касание добавляет или убирает существо из списка.</summary>
    private void OnLinkInteract(Entity<SlimeCrossLinkComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<MobStateComponent>(target))
            return;
        if (TryComp<SlimeCrossActivatedComponent>(ent, out var activated) && activated.Active)
            return;

        args.Handled = true;
        if (ent.Comp.Linked.Remove(target))
            PopupUser("slime-cross-unlink", args.User, ("extract", ent.Owner), ("target", target));
        else
        {
            ent.Comp.Linked.Add(target);
            PopupUser("slime-cross-link", args.User, ("extract", ent.Owner), ("target", target));
        }
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        UpdateScheduled(now);
        UpdatePassive(now, frameTime);
        UpdateStabilized(now);
        UpdateEffects(now);
        UpdateItems(now);
    }

    private readonly List<(TimeSpan At, Action Act)> _scheduled = new();

    private void Schedule(TimeSpan delay, Action act)
    {
        _scheduled.Add((_timing.CurTime + delay, act));
    }

    private void UpdateScheduled(TimeSpan now)
    {
        for (var i = _scheduled.Count - 1; i >= 0; i--)
        {
            if (now < _scheduled[i].At)
                continue;
            var act = _scheduled[i].Act;
            _scheduled.RemoveAt(i);
            act();
        }
    }
}
