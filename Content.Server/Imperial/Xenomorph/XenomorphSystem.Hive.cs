using System.Linq;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Xenomorph;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Xenomorph;

public sealed partial class XenomorphSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly EntProtoId WeedNode = "ImperialXenoWeedNode";
    private static readonly EntProtoId Egg = "ImperialXenoEgg";
    private static readonly EntProtoId RoyalParasite = "ImperialXenoRoyalParasite";
    private static readonly SoundSpecifier ResinSound = new SoundPathSpecifier("/Audio/Imperial/Xenomorph/attackblob.ogg");

    /// <summary>Secrete Resin: 55 плазмы.</summary>
    private const float ResinCost = 55;

    private static readonly EntProtoId[] ResinStructures =
    {
        "ImperialXenoResinWall",
        "ImperialXenoResinMembrane",
        "ImperialXenoNest",
    };

    private static readonly SpriteSpecifier[] ResinIcons =
    {
        new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/Xenomorph/resin_wall.rsi"), "wall"),
        new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/Xenomorph/resin_membrane.rsi"), "membrane"),
        new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/Xenomorph/nest.rsi"), "nest"),
    };

    /// <summary>Тех, кто выбрался из гнезда сам, отпускаем без повторной проверки.</summary>
    private readonly HashSet<EntityUid> _nestEscaping = new();

    private TimeSpan _nextEggCheck;
    private TimeSpan _nextHeatCheck;

    private static readonly TimeSpan HeatCheckInterval = TimeSpan.FromSeconds(1);
    private const float WeedsBurnTemperature = 300;
    private const float EggBurnTemperature = 500;

    /// <summary>egg/integrity_failure = 0.05: при 95 уроне яйцо лопается, а лицехват внутри погибает.</summary>
    private const float EggBreakDamage = 95;

    private void InitializeHive()
    {
        SubscribeLocalEvent<XenomorphComponent, XenoPlantWeedsActionEvent>(OnPlantWeeds);
        SubscribeLocalEvent<XenomorphComponent, XenoSecreteResinActionEvent>(OnSecreteResin);
        SubscribeLocalEvent<XenomorphComponent, XenoLayEggActionEvent>(OnLayEgg);
        SubscribeLocalEvent<XenomorphComponent, XenoRoyalParasiteActionEvent>(OnRoyalParasite);
        SubscribeLocalEvent<XenoRoyalParasiteComponent, AfterInteractEvent>(OnRoyalParasiteUse);
        SubscribeLocalEvent<XenoRoyalParasiteComponent, GotUnequippedHandEvent>(OnRoyalParasiteDropped);

        SubscribeLocalEvent<XenoWeedNodeComponent, MapInitEvent>(OnNodeMapInit);
        SubscribeLocalEvent<XenoWeedNodeComponent, ComponentShutdown>(OnNodeShutdown);

        SubscribeLocalEvent<XenoNestComponent, StrapAttemptEvent>(OnNestStrapAttempt);
        SubscribeLocalEvent<XenoNestComponent, UnstrapAttemptEvent>(OnNestUnstrapAttempt);
        SubscribeLocalEvent<XenoNestComponent, XenoNestEscapeDoAfterEvent>(OnNestEscape);

        SubscribeLocalEvent<XenoEggComponent, MapInitEvent>(OnEggMapInit);
        SubscribeLocalEvent<XenoEggComponent, InteractHandEvent>(OnEggInteract);
        SubscribeLocalEvent<XenoEggComponent, DamageChangedEvent>(OnEggDamaged);
    }

    private void UpdateHive(TimeSpan now)
    {
        var nodes = EntityQueryEnumerator<XenoWeedNodeComponent>();
        while (nodes.MoveNext(out var uid, out var node))
        {
            if (now < node.NextGrow)
                continue;

            node.NextGrow = now + RandomGrowTime(node);
            GrowWeeds((uid, node));
        }

        // atmos_expose: сорняки горят выше 300 K, яйца — выше 500 K (5 урона огнём за атмос-тик).
        var heatCheck = now >= _nextHeatCheck;
        if (heatCheck)
            _nextHeatCheck = now + HeatCheckInterval;

        var weeds = EntityQueryEnumerator<XenoWeedsComponent>();
        while (weeds.MoveNext(out var uid, out var weed))
        {
            if (weed.DieAt is { } dieAt && now >= dieAt)
            {
                QueueDel(uid);
                continue;
            }

            if (heatCheck)
                BurnIfHot(uid, WeedsBurnTemperature);
        }

        var eggs = EntityQueryEnumerator<XenoEggComponent>();
        var checkHosts = now >= _nextEggCheck;
        if (checkHosts)
            _nextEggCheck = now + TimeSpan.FromSeconds(0.5);

        while (eggs.MoveNext(out var uid, out var egg))
        {
            if (heatCheck)
                BurnIfHot(uid, EggBurnTemperature);

            UpdateEgg((uid, egg), now, checkHosts);
        }
    }

    private void BurnIfHot(EntityUid uid, float temperature)
    {
        if (_atmos.GetContainingMixture(uid) is { } air && air.Temperature > temperature)
            _damageable.TryChangeDamage(uid, new DamageSpecifier { DamageDict = { ["Heat"] = 5 } });
    }

    private bool TryGetTile(EntityUid uid, out EntityUid grid, out MapGridComponent gridComp, out Vector2i tile)
    {
        grid = default;
        gridComp = default!;
        tile = default;
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? comp))
            return false;

        grid = gridUid;
        gridComp = comp;
        tile = _map.TileIndicesFor(gridUid, comp, xform.Coordinates);
        return !_turf.IsSpace(_map.GetTileRef(gridUid, comp, tile));
    }

    private bool TileHas<T>(EntityUid grid, MapGridComponent gridComp, Vector2i tile) where T : IComponent
    {
        foreach (var anchored in _map.GetAnchoredEntities(grid, gridComp, tile))
        {
            if (HasComp<T>(anchored))
                return true;
        }

        return false;
    }

    #region Сорняки

    private TimeSpan RandomGrowTime(XenoWeedNodeComponent node)
    {
        return TimeSpan.FromSeconds(_random.NextFloat((float) node.MinGrowTime.TotalSeconds, (float) node.MaxGrowTime.TotalSeconds));
    }

    /// <summary>Plant Weeds: узел на клетке, где его ещё нет.</summary>
    private void OnPlantWeeds(Entity<XenomorphComponent> ent, ref XenoPlantWeedsActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetTile(ent, out var grid, out var gridComp, out var tile))
        {
            _popup.PopupEntity(Loc.GetString("xeno-build-invalid-tile"), ent, ent);
            return;
        }

        if (TileHas<XenoWeedNodeComponent>(grid, gridComp, tile))
        {
            _popup.PopupEntity(Loc.GetString("xeno-weeds-already"), ent, ent);
            return;
        }

        if (!TrySpendPlasma(ent, args.PlasmaCost))
            return;

        args.Handled = true;
        Spawn(WeedNode, _map.GridTileToLocal(grid, gridComp, tile));
        _popup.PopupEntity(Loc.GetString("xeno-weeds-planted", ("xeno", ent.Owner)), ent, PopupType.Small);
    }

    private void OnNodeMapInit(Entity<XenoWeedNodeComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextGrow = _timing.CurTime + RandomGrowTime(ent.Comp);

        if (TryComp<XenoWeedsComponent>(ent, out var self))
            self.Node = ent;

        // Узел заменяет обычный сорняк на своей клетке.
        if (!TryGetTile(ent, out var grid, out var gridComp, out var tile))
            return;

        foreach (var anchored in _map.GetAnchoredEntities(grid, gridComp, tile).ToArray())
        {
            if (anchored != ent.Owner && HasComp<XenoWeedsComponent>(anchored) && !HasComp<XenoWeedNodeComponent>(anchored))
                QueueDel(anchored);
        }
    }

    /// <summary>node/process: каждый сорняк в радиусе узла пробует разрастись на соседние клетки.</summary>
    private void GrowWeeds(Entity<XenoWeedNodeComponent> node)
    {
        if (!TryGetTile(node, out var grid, out var gridComp, out var center))
            return;

        var growing = new List<Vector2i>();
        var weeds = EntityQueryEnumerator<XenoWeedsComponent, TransformComponent>();
        while (weeds.MoveNext(out _, out var weed, out var xform))
        {
            if (weed.Node != node.Owner || xform.GridUid != grid)
                continue;

            var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
            if (Math.Max(Math.Abs(tile.X - center.X), Math.Abs(tile.Y - center.Y)) <= node.Comp.Range)
                growing.Add(tile);
        }

        foreach (var tile in growing)
        {
            foreach (var offset in new[] { new Vector2i(1, 0), new Vector2i(-1, 0), new Vector2i(0, 1), new Vector2i(0, -1) })
            {
                var target = tile + offset;
                if (!_map.TryGetTileRef(grid, gridComp, target, out var tileRef)
                    || _turf.IsSpace(tileRef)
                    || _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable)
                    || TileHas<XenoWeedsComponent>(grid, gridComp, target))
                {
                    continue;
                }

                var spawned = Spawn(_random.Pick(node.Comp.Weeds), _map.GridTileToLocal(grid, gridComp, target));
                if (TryComp<XenoWeedsComponent>(spawned, out var weed))
                    weed.Node = node;
            }
        }
    }

    /// <summary>after_parent_destroyed: ищем другой узел в радиусе, иначе засыхаем через 2–8 с.</summary>
    private void OnNodeShutdown(Entity<XenoWeedNodeComponent> ent, ref ComponentShutdown args)
    {
        var now = _timing.CurTime;
        var weeds = EntityQueryEnumerator<XenoWeedsComponent, TransformComponent>();
        while (weeds.MoveNext(out var uid, out var weed, out var xform))
        {
            if (weed.Node != ent.Owner || uid == ent.Owner)
                continue;

            weed.Node = null;
            foreach (var other in _lookup.GetEntitiesInRange<XenoWeedNodeComponent>(xform.Coordinates, ent.Comp.Range))
            {
                if (other.Owner == ent.Owner || TerminatingOrDeleted(other))
                    continue;

                weed.Node = other;
                break;
            }

            if (weed.Node == null)
                weed.DieAt = now + TimeSpan.FromSeconds(_random.NextFloat(2, 8));
        }
    }

    #endregion

    #region Смола

    private void OnSecreteResin(Entity<XenomorphComponent> ent, ref XenoSecreteResinActionEvent args)
    {
        if (args.Handled)
            return;

        if (!HasPlasma(ent, ResinCost))
        {
            _popup.PopupEntity(Loc.GetString("xeno-not-enough-plasma"), ent, ent);
            return;
        }

        args.Handled = true;
        var options = new List<XenoRadialOption>();
        for (var i = 0; i < ResinStructures.Length; i++)
        {
            options.Add(new XenoRadialOption(ResinStructures[i], _proto.Index(ResinStructures[i]).Name, ResinIcons[i]));
        }

        OpenRadial(ent, XenoRadialUiKey.Resin, options);
    }

    /// <summary>make_structure/resin: постройка на своей клетке, если там нет другой смолы.</summary>
    private void SecreteResin(EntityUid uid, string structure)
    {
        if (!ResinStructures.Any(s => s.Id == structure))
            return;

        if (!TryGetTile(uid, out var grid, out var gridComp, out var tile))
        {
            _popup.PopupEntity(Loc.GetString("xeno-build-invalid-tile"), uid, uid);
            return;
        }

        foreach (var anchored in _map.GetAnchoredEntities(grid, gridComp, tile))
        {
            if (MetaData(anchored).EntityPrototype?.ID is { } id && ResinStructures.Any(s => s.Id == id))
            {
                _popup.PopupEntity(Loc.GetString("xeno-resin-already"), uid, uid);
                return;
            }
        }

        if (!TrySpendPlasma(uid, ResinCost))
            return;

        Spawn(structure, _map.GridTileToLocal(grid, gridComp, tile));
        _audio.PlayPvs(ResinSound, uid);
        _popup.PopupEntity(Loc.GetString("xeno-resin-secreted", ("xeno", uid)), uid, PopupType.Small);
    }

    #endregion

    #region Гнездо

    /// <summary>user_buckle_mob: пристегнуть к гнезду может только ксеноморф, и не другого ксеноморфа.</summary>
    private void OnNestStrapAttempt(Entity<XenoNestComponent> ent, ref StrapAttemptEvent args)
    {
        if (args.User is { } user && HasComp<XenomorphComponent>(user) && user != args.Buckle.Owner
            && !HasComp<XenoPlasmaComponent>(args.Buckle.Owner))
        {
            return;
        }

        args.Cancelled = true;
        if (args.User is not { } u || !args.Popup)
            return;

        // Ксеноморфа (с плазменным сосудом) в гнездо не уложить — гнездо для добычи.
        var message = HasComp<XenoPlasmaComponent>(args.Buckle.Owner) ? "xeno-nest-not-for-xeno" : "xeno-nest-only-xeno";
        _popup.PopupEntity(Loc.GetString(message), ent, u);
    }

    /// <summary>Пленнику самому выбираться 100 секунд; ксеноморфы и спасатели снимают сразу.</summary>
    private void OnNestUnstrapAttempt(Entity<XenoNestComponent> ent, ref UnstrapAttemptEvent args)
    {
        var captive = args.Buckle.Owner;
        if (_nestEscaping.Remove(captive))
            return;

        if (args.User != captive)
            return;

        args.Cancelled = true;
        if (HasComp<XenomorphComponent>(captive))
            return;

        _popup.PopupEntity(Loc.GetString("xeno-nest-struggle"), ent, captive);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, captive, ent.Comp.EscapeTime, new XenoNestEscapeDoAfterEvent(), ent, captive, ent)
        {
            BreakOnMove = false,
            NeedHand = false,
            Hidden = true,
        });
    }

    private void OnNestEscape(Entity<XenoNestComponent> ent, ref XenoNestEscapeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } captive)
            return;

        args.Handled = true;
        _nestEscaping.Add(captive);
        _buckle.Unbuckle(captive, captive);
        _nestEscaping.Remove(captive);
        _popup.PopupEntity(Loc.GetString("xeno-nest-escaped", ("captive", captive)), ent, PopupType.Medium);
    }

    #endregion

    #region Яйца

    /// <summary>Lay Egg (75): только королева.</summary>
    private void OnLayEgg(Entity<XenomorphComponent> ent, ref XenoLayEggActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetTile(ent, out var grid, out var gridComp, out var tile))
        {
            _popup.PopupEntity(Loc.GetString("xeno-build-invalid-tile"), ent, ent);
            return;
        }

        if (TileHas<XenoEggComponent>(grid, gridComp, tile))
        {
            _popup.PopupEntity(Loc.GetString("xeno-egg-already"), ent, ent);
            return;
        }

        if (!TrySpendPlasma(ent, args.PlasmaCost))
            return;

        args.Handled = true;
        Spawn(Egg, _map.GridTileToLocal(grid, gridComp, tile));
        _popup.PopupEntity(Loc.GetString("xeno-egg-laid", ("xeno", ent.Owner)), ent, PopupType.Medium);
    }

    private void OnEggMapInit(Entity<XenoEggComponent> ent, ref MapInitEvent args)
    {
        SetEggState(ent, ent.Comp.StartGrown ? XenoEggState.Grown : XenoEggState.Growing);
        if (!ent.Comp.StartGrown)
        {
            ent.Comp.NextStateAt = _timing.CurTime + TimeSpan.FromSeconds(
                _random.NextFloat((float) ent.Comp.MinGrowth.TotalSeconds, (float) ent.Comp.MaxGrowth.TotalSeconds));
        }
    }

    private void SetEggState(Entity<XenoEggComponent> ent, XenoEggState state)
    {
        ent.Comp.State = state;
        _appearance.SetData(ent, XenoVisuals.EggState, state);
    }

    private void UpdateEgg(Entity<XenoEggComponent> ent, TimeSpan now, bool checkHosts)
    {
        switch (ent.Comp.State)
        {
            case XenoEggState.Growing when now >= ent.Comp.NextStateAt:
                SetEggState(ent, XenoEggState.Grown);
                break;
            case XenoEggState.Grown when checkHosts:
                // egg/HasProximity: CanHug, но заражённый в сознании яйцо не тревожит.
                foreach (var mob in _lookup.GetEntitiesInRange<Content.Shared.Mobs.Components.MobStateComponent>(Transform(ent).Coordinates, ent.Comp.TriggerRange))
                {
                    if (!CanHug(mob) || HasComp<XenoEmbryoComponent>(mob) && _mobState.IsAlive(mob))
                        continue;

                    OpenEgg(ent, kill: false);
                    break;
                }
                break;
            case XenoEggState.Opening when now >= ent.Comp.NextStateAt:
                SetEggState(ent, XenoEggState.Hatched);
                FinishBursting(ent);
                break;
        }
    }

    /// <summary>egg/Burst: 1.5 секунды анимации, затем выходит лицехват.</summary>
    private void OpenEgg(Entity<XenoEggComponent> ent, bool kill)
    {
        if (ent.Comp.State is not (XenoEggState.Grown or XenoEggState.Growing))
            return;

        ent.Comp.KillChild = kill;
        SetEggState(ent, XenoEggState.Opening);
        ent.Comp.NextStateAt = _timing.CurTime + TimeSpan.FromSeconds(1.5);
        _audio.PlayPvs(ResinSound, ent);
    }

    /// <summary>finish_bursting: живой лицехват сразу прыгает на соседнего носителя, разбитое яйцо даёт мёртвого.</summary>
    private void FinishBursting(Entity<XenoEggComponent> ent)
    {
        var hugger = Spawn(ent.Comp.Facehugger, Transform(ent).Coordinates);
        if (!TryComp<XenoFacehuggerComponent>(hugger, out var comp))
            return;

        if (ent.Comp.KillChild)
        {
            Die((hugger, comp));
            return;
        }

        foreach (var mob in _lookup.GetEntitiesInRange<Content.Shared.Mobs.Components.MobStateComponent>(Transform(ent).Coordinates, ent.Comp.TriggerRange))
        {
            if (CanHug(mob) && Leap((hugger, comp), mob))
                break;
        }
    }

    /// <summary>egg/atom_break: при 5 % прочности яйцо лопается, лицехват погибает.</summary>
    private void OnEggDamaged(Entity<XenoEggComponent> ent, ref DamageChangedEvent args)
    {
        if (_damageable.GetTotalDamage(ent.Owner) >= EggBreakDamage)
            OpenEgg(ent, kill: true);
    }

    /// <summary>egg/attack_hand: ксеноморф достаёт созревшего лицехвата или убирает пустое яйцо.</summary>
    private void OnEggInteract(Entity<XenoEggComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!HasComp<XenoPlasmaComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("xeno-egg-slimy"), ent, args.User);
            return;
        }

        switch (ent.Comp.State)
        {
            case XenoEggState.Grown:
                _popup.PopupEntity(Loc.GetString("xeno-egg-retrieve"), ent, args.User);
                OpenEgg(ent, kill: false);
                break;
            case XenoEggState.Growing:
                _popup.PopupEntity(Loc.GetString("xeno-egg-growing"), ent, args.User);
                break;
            case XenoEggState.Opening:
                _popup.PopupEntity(Loc.GetString("xeno-egg-hatching"), ent, args.User);
                break;
            case XenoEggState.Hatched:
                _popup.PopupEntity(Loc.GetString("xeno-egg-clear"), ent, args.User);
                _audio.PlayPvs(ResinSound, ent);
                QueueDel(ent);
                break;
        }
    }

    #endregion

    #region Королевский паразит

    /// <summary>
    /// alien/promote: плазма списывается только при возвышении. Повторное нажатие выбрасывает паразита.
    /// Недоступно, пока есть преторианец или плазмы меньше 500.
    /// </summary>
    private void OnRoyalParasite(Entity<XenomorphComponent> ent, ref XenoRoyalParasiteActionEvent args)
    {
        if (args.Handled)
            return;

        foreach (var held in _hands.EnumerateHeld(ent.Owner).ToArray())
        {
            if (!HasComp<XenoRoyalParasiteComponent>(held))
                continue;

            args.Handled = true;
            QueueDel(held);
            _popup.PopupEntity(Loc.GetString("xeno-royal-parasite-discard"), ent, ent);
            return;
        }

        if (!HasPlasma(ent, args.PlasmaCost))
        {
            _popup.PopupEntity(Loc.GetString("xeno-not-enough-plasma"), ent, ent);
            return;
        }

        if (IsCasteAlive(XenoCaste.Praetorian))
        {
            _popup.PopupEntity(Loc.GetString("xeno-royal-parasite-exists"), ent, ent);
            return;
        }

        if (!_hands.TryGetEmptyHand(ent.Owner, out _))
        {
            _popup.PopupEntity(Loc.GetString("xeno-royal-parasite-no-hand"), ent, ent);
            return;
        }

        args.Handled = true;
        var parasite = Spawn(RoyalParasite, Transform(ent).Coordinates);
        if (!_hands.TryPickupAnyHand(ent, parasite))
        {
            QueueDel(parasite);
            return;
        }

        Comp<XenoRoyalParasiteComponent>(parasite).Cost = args.PlasmaCost;
        _popup.PopupEntity(Loc.GetString("xeno-royal-parasite-created"), ent, ent);
    }

    /// <summary>queen_promotion/attack: возвысить взрослого некоролевского ребёнка с разумом до преторианца.</summary>
    private void OnRoyalParasiteUse(Entity<XenoRoyalParasiteComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!TryComp<XenomorphComponent>(target, out var xeno)
            || xeno.Caste is XenoCaste.Larva or XenoCaste.Praetorian or XenoCaste.Queen)
        {
            _popup.PopupEntity(Loc.GetString("xeno-royal-parasite-invalid"), target, args.User);
            return;
        }

        // promote/IsAvailable: только у королевы с 500 плазмы и пока нет преторианца.
        if (!TryComp<XenomorphComponent>(args.User, out var user) || user.Caste != XenoCaste.Queen)
        {
            _popup.PopupEntity(Loc.GetString("xeno-royal-parasite-not-queen"), target, args.User);
            return;
        }

        if (IsCasteAlive(XenoCaste.Praetorian))
        {
            _popup.PopupEntity(Loc.GetString("xeno-royal-parasite-exists"), target, args.User);
            return;
        }

        if (!HasPlasma(args.User, ent.Comp.Cost))
        {
            _popup.PopupEntity(Loc.GetString("xeno-not-enough-plasma"), target, args.User);
            return;
        }

        // queen_promotion/attack: to_promote.stat == CONSCIOUS, есть mind и key.
        if (!_mobState.IsAlive(target))
        {
            _popup.PopupEntity(Loc.GetString("xeno-royal-parasite-not-conscious", ("target", target)), target, args.User);
            return;
        }

        if (!_mind.TryGetMind(target, out _, out var mind) || mind.UserId == null)
        {
            _popup.PopupEntity(Loc.GetString("xeno-royal-parasite-no-mind", ("target", target)), target, args.User);
            return;
        }

        TrySpendPlasma(args.User, ent.Comp.Cost);
        _popup.PopupEntity(Loc.GetString("xeno-royal-parasite-promoted", ("target", target)), args.User, args.User);
        QueueDel(ent);
        Evolve(target, ent.Comp.Praetorian);
    }

    /// <summary>queen_promotion/dropped: паразит исчезает, если его выпустить из рук (DROPDEL).</summary>
    private void OnRoyalParasiteDropped(Entity<XenoRoyalParasiteComponent> ent, ref GotUnequippedHandEvent args)
    {
        QueueDel(ent);
    }

    #endregion
}
