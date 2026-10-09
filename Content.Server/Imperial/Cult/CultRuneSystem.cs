using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Ghost;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Imperial.Cult.Components;
using Content.Server.Imperial.Lavaland.Storm;
using Content.Server.Mind;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Gibbing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.Station;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Руны культа (runes.dm) и ритуальный предмет (datum/component/cult_ritual_item).
/// </summary>
public sealed class CultRuneSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly CultSystem _cult = default!;
    [Dependency] private readonly ImperialCultRuleSystem _rule = default!;
    [Dependency] private readonly CultBloodMagicSystem _magic = default!;
    [Dependency] private readonly CultConstructSystem _constructs = default!;
    [Dependency] private readonly CultStructureSystem _structures = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffs = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationSpawningSystem _spawning = default!;
    [Dependency] private readonly Content.Server.GameTicking.GameTicker _ticker = default!;

    public static readonly Color ColorTalisman = Color.FromHex("#0000FF");
    public static readonly Color ColorTeleport = Color.FromHex("#551A8B");
    public static readonly Color ColorDarkRed = Color.FromHex("#7D1717");
    public static readonly Color ColorMediumRed = Color.FromHex("#C80000");
    public static readonly Color ColorRed = Color.FromHex("#FF0000");

    private const string RuneSpawnEffect = "CultRuneSpawnEffect";
    private const int GhostLimit = 3;

    private List<(EntProtoId Id, CultRuneComponent Rune, string Name)>? _scribable;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultRuneComponent, MapInitEvent>(OnRuneMapInit);
        SubscribeLocalEvent<CultRuneComponent, ExaminedEvent>(OnRuneExamined);
        SubscribeLocalEvent<CultRuneComponent, ActivateInWorldEvent>(OnRuneActivate);
        SubscribeLocalEvent<CultRuneComponent, ComponentShutdown>(OnRuneShutdown);

        SubscribeLocalEvent<CultRitualItemComponent, UseInHandEvent>(OnRitualUse);
        SubscribeLocalEvent<CultRitualItemComponent, AfterInteractEvent>(OnRitualAfterInteract);
        SubscribeLocalEvent<CultRitualItemComponent, ExaminedEvent>(OnRitualExamined);
        SubscribeLocalEvent<CultistComponent, CultScribeRuneDoAfterEvent>(OnScribeDoAfter);
        SubscribeLocalEvent<CultistComponent, CultEraseRuneDoAfterEvent>(OnEraseDoAfter);

        SubscribeLocalEvent<CultBarrierComponent, ActivateInWorldEvent>(OnBarrierActivate);
        SubscribeLocalEvent<CultBarrierComponent, ComponentShutdown>(OnBarrierShutdown);

        SubscribeLocalEvent<CultSpiritGhostComponent, MobStateChangedEvent>(OnSpiritGhostState);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _scribable = null);
    }

    /// <summary>Совместимость со старым CultRuleSystem: сообщение всем культистам.</summary>
    public void BroadcastCultMessage(string message)
    {
        _cult.MessageCult(message);
    }

    #region Руна: общее

    private void OnRuneMapInit(Entity<CultRuneComponent> ent, ref MapInitEvent args)
    {
        var color = ent.Comp.RuneColor;
        if (ent.Comp.RuneType == CultRuneType.Malformed)
        {
            // Цвет искажённой руны — общий красный (просьба сервера), узор случайный.
            _appearance.SetData(ent, CultVisuals.State, $"rune{_random.Next(1, 9)}");
        }
        ent.Comp.RuneColor = color;
        _appearance.SetData(ent, CultVisuals.Color, color);
    }

    private void OnRuneShutdown(Entity<CultRuneComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Barrier is { } barrier && !TerminatingOrDeleted(barrier))
            QueueDel(barrier);
        ClosePortal(ent);
    }

    private void OnRuneExamined(Entity<CultRuneComponent> ent, ref ExaminedEvent args)
    {
        if (!_cult.IsCultAligned(args.Examiner) && !HasComp<GhostComponent>(args.Examiner))
            return;

        var req = ent.Comp.ReqCultistsText != null ? Loc.GetString(ent.Comp.ReqCultistsText.Value) : ent.Comp.ReqCultists.ToString();
        args.PushMarkup(Loc.GetString("cult-rune-examine", ("name", Loc.GetString(ent.Comp.CultistName))));
        args.PushMarkup(Loc.GetString("cult-rune-examine-effect", ("desc", Loc.GetString(ent.Comp.CultistDesc))));
        args.PushMarkup(Loc.GetString("cult-rune-examine-req", ("req", req)));
        if (ent.Comp.ReqKeyword && ent.Comp.Keyword != null)
            args.PushMarkup(Loc.GetString("cult-rune-examine-keyword", ("keyword", ent.Comp.Keyword)));
        if (ent.Comp.RuneType == CultRuneType.Revive && _rule.TryGetRule(out var rule))
            args.PushMarkup(Loc.GetString("cult-rune-examine-sacrifices", ("count", rule.Value.Comp.Sacrificed - rule.Value.Comp.SacrificesUsed - rule.Value.Comp.SoulsToRevive)));
        if (ent.Comp.PortalDescription != null)
            args.PushMarkup(ent.Comp.PortalDescription);
    }

    private void OnRuneActivate(Entity<CultRuneComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var user = args.User;

        if (!_cult.IsCultAligned(user))
        {
            _popup.PopupEntity(Loc.GetString("cult-rune-cant-understand", ("rune", ent)), user, user);
            return;
        }

        // attack_animal: конструкт, которому нельзя призывать.
        if (!HasComp<CultistComponent>(user) && !ent.Comp.ConstructInvoke)
        {
            _popup.PopupEntity(Loc.GetString("cult-rune-construct-cant"), user, user);
            return;
        }

        TryInvoke(ent, user);
    }

    public void TryInvoke(Entity<CultRuneComponent> rune, EntityUid user)
    {
        // can_invoke руны Царства духов.
        if (rune.Comp.RuneType == CultRuneType.SpiritRealm)
        {
            if (!OnRuneTile(rune, user))
            {
                _popup.PopupEntity(Loc.GetString("cult-rune-manifest-stand"), user, user);
                FailInvoke(rune);
                return;
            }
            if (TryComp<CultistComponent>(user, out var c) && c.CultGhost)
            {
                _popup.PopupEntity(Loc.GetString("cult-rune-manifest-ghost"), user, user);
                FailInvoke(rune);
                return;
            }
        }

        var invokers = CanInvoke(rune, user);
        if (invokers.Count >= rune.Comp.ReqCultists)
        {
            Invoke(rune, invokers);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cult-rune-need-more", ("count", rune.Comp.ReqCultists - invokers.Count)), user, user, PopupType.MediumCaution);
        FailInvoke(rune);
    }

    /// <summary>can_invoke(): пользователь + культисты в range(1), говорящие и в сознании.</summary>
    public List<EntityUid> CanInvoke(Entity<CultRuneComponent> rune, EntityUid user)
    {
        var invokers = new List<EntityUid> { user };
        if (rune.Comp.ReqCultists <= 1 && rune.Comp.RuneType != CultRuneType.Offer)
            return invokers;

        foreach (var other in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(rune).Coordinates, 1.5f))
        {
            var uid = other.Owner;
            if (uid == user || !_cult.IsCultAligned(uid))
                continue;
            if (HasComp<MutedComponent>(uid) || !_mobState.IsAlive(uid) || HasComp<SleepingComponent>(uid))
                continue;
            // shade/check_invoke_validity(): в камне или меньше минуты после выхода.
            if (TryComp<CultShadeComponent>(uid, out var shade))
            {
                if (shade.ReleaseTime == null)
                {
                    _cult.Message(uid, Loc.GetString("cult-shade-in-stone"));
                    continue;
                }
                if (shade.ReleaseTime.Value + TimeSpan.FromMinutes(1) > _timing.CurTime)
                {
                    _cult.Message(uid, Loc.GetString("cult-shade-too-weak"));
                    continue;
                }
            }
            invokers.Add(uid);
        }
        return invokers;
    }

    /// <summary>obj/effect/rune/invoke(): каждый произносит заклинание, урон, вспышка.</summary>
    private void BaseInvoke(Entity<CultRuneComponent> rune, List<EntityUid> invokers, bool glow = true)
    {
        foreach (var invoker in invokers)
        {
            if (TerminatingOrDeleted(invoker))
                continue;
            if (!string.IsNullOrEmpty(rune.Comp.Invocation))
                _cult.Say(invoker, rune.Comp.Invocation);
            if (rune.Comp.InvokeDamage > 0)
            {
                _cult.Damage(invoker, "Blunt", rune.Comp.InvokeDamage);
                _cult.Message(invoker, Loc.GetString("cult-rune-saps", ("rune", rune)));
            }
        }

        if (glow)
            DoInvokeGlow(rune);
    }

    /// <summary>do_invoke_glow(): x2 и прозрачность за 0.5 с.</summary>
    public void DoInvokeGlow(Entity<CultRuneComponent> rune)
    {
        rune.Comp.GlowCounter++;
        _appearance.SetData(rune, CultVisuals.InvokeGlow, rune.Comp.GlowCounter);
    }

    /// <summary>fail_invoke(): красная вспышка.</summary>
    public void FailInvoke(Entity<CultRuneComponent> rune)
    {
        _popup.PopupEntity(Loc.GetString("cult-rune-fail"), rune, PopupType.SmallCaution);
        rune.Comp.FailCounter++;
        _appearance.SetData(rune, CultVisuals.FailFlash, rune.Comp.FailCounter);

        if (rune.Comp.RuneType == CultRuneType.Revive)
        {
            rune.Comp.InUse = false;
            foreach (var mob in EntitiesOnRune(rune))
            {
                if (_cult.IsCultist(mob) && _mobState.IsDead(mob))
                    _popup.PopupEntity(Loc.GetString("cult-rune-revive-twitch", ("target", mob)), mob, PopupType.Small);
            }
        }
    }

    private void SetRuneColor(Entity<CultRuneComponent> rune, Color color)
    {
        _appearance.SetData(rune, CultVisuals.Color, color);
    }

    private void Invoke(Entity<CultRuneComponent> rune, List<EntityUid> invokers)
    {
        switch (rune.Comp.RuneType)
        {
            case CultRuneType.Malformed:
                BaseInvoke(rune, invokers);
                QueueDel(rune);
                break;
            case CultRuneType.Offer:
                InvokeOffer(rune, invokers);
                break;
            case CultRuneType.Empower:
                BaseInvoke(rune, invokers);
                _magic.OpenPrepare(invokers[0], onRune: true);
                break;
            case CultRuneType.Teleport:
                InvokeTeleport(rune, invokers);
                break;
            case CultRuneType.NarSie:
                InvokeNarSie(rune, invokers);
                break;
            case CultRuneType.Revive:
                InvokeRevive(rune, invokers);
                break;
            case CultRuneType.Barrier:
                InvokeBarrier(rune, invokers);
                break;
            case CultRuneType.Summon:
                InvokeSummon(rune, invokers);
                break;
            case CultRuneType.BloodBoil:
                InvokeBloodBoil(rune, invokers);
                break;
            case CultRuneType.SpiritRealm:
                InvokeSpiritRealm(rune, invokers);
                break;
            case CultRuneType.Apocalypse:
                InvokeApocalypse(rune, invokers);
                break;
        }
    }

    private bool OnRuneTile(EntityUid rune, EntityUid mob)
    {
        var a = _transform.GetMapCoordinates(rune);
        var b = _transform.GetMapCoordinates(mob);
        return a.MapId == b.MapId && (a.Position - b.Position).Length() < 0.6f && Transform(mob).ParentUid == Transform(rune).ParentUid;
    }

    /// <summary>mob/living in loc.</summary>
    private List<EntityUid> EntitiesOnRune(EntityUid rune)
    {
        var result = new List<EntityUid>();
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(rune).Coordinates, 0.5f))
        {
            if (Transform(mob).ParentUid == Transform(rune).ParentUid)
                result.Add(mob.Owner);
        }
        return result;
    }

    #endregion

    #region Подношение

    private void InvokeOffer(Entity<CultRuneComponent> rune, List<EntityUid> invokers)
    {
        if (rune.Comp.InUse)
            return;

        var targets = EntitiesOnRune(rune).Where(m => !_cult.IsCultist(m)).ToList();
        if (targets.Count == 0)
        {
            FailInvoke(rune);
            return;
        }

        rune.Comp.InUse = true;
        _popup.PopupEntity(Loc.GetString("cult-rune-offer-pulse", ("rune", rune)), rune, PopupType.MediumCaution);
        SetRuneColor(rune, ColorDarkRed);

        var target = _random.Pick(targets);
        if (!_mobState.IsDead(target) && _cult.IsConvertable(target))
        {
            rune.Comp.Invocation = "Mah'weyh pleggh at e'ntrath!";
            BaseInvoke(rune, invokers, glow: false);
            DoConvert(rune, target, invokers);
        }
        else
        {
            rune.Comp.Invocation = "Barhah hra zar'garis!";
            BaseInvoke(rune, invokers, glow: false);
            DoSacrifice(rune, target, invokers);
        }

        _cult.CheckSize();

        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(0.5), () =>
        {
            if (!TerminatingOrDeleted(rune))
                SetRuneColor(rune, rune.Comp.RuneColor);
        });
        rune.Comp.InUse = false;
    }

    private bool DoConvert(Entity<CultRuneComponent> rune, EntityUid target, List<EntityUid> invokers)
    {
        if (invokers.Count < 2)
        {
            foreach (var invoker in invokers)
                _cult.Message(invoker, Loc.GetString("cult-rune-offer-need-two", ("target", target)));
            return false;
        }

        if (_cult.BlocksMagic(target, holy: true))
        {
            foreach (var invoker in invokers)
                _cult.Message(invoker, Loc.GetString("cult-rune-offer-shielded", ("target", target)));
            return false;
        }

        var brute = _cult.GetGroupDamage(target, "Brute");
        var burn = _cult.GetGroupDamage(target, "Burn");
        _cult.HealGroup(target, "Brute", brute * 0.75f);
        _cult.HealGroup(target, "Burn", burn * 0.75f);

        var healed = brute > 0 || burn > 0;
        _popup.PopupEntity(Loc.GetString(healed ? "cult-rune-offer-writhes-heal" : "cult-rune-offer-writhes", ("target", target)),
            target, Filter.PvsExcept(target), true, PopupType.MediumCaution);
        _cult.Message(target, Loc.GetString("cult-rune-offer-scream"));

        if (HasComp<HumanoidProfileComponent>(target))
        {
            if (TryComp<CuffableComponent>(target, out var cuffable))
            {
                foreach (var cuff in cuffable.Container.ContainedEntities.ToList())
                    _cuffs.Uncuff(target, null, cuff, cuffable);
            }
            _cult.ClearSpeechEffects(target);
        }

        _cult.Unconscious(target, 10);
        Spawn("CultDagger", Transform(rune).Coordinates);
        _cult.AddCultist(target, converted: true, silent: true);
        _cult.SoundTo(target, CultSounds.BloodcultGain);
        _cult.Message(target, Loc.GetString("cult-rune-offer-converted-1"));
        _cult.Message(target, Loc.GetString("cult-rune-offer-converted-2"));
        _cult.MessageCult(Loc.GetString("cult-commune-converted", ("name", Name(target))));
        return true;
    }

    private bool DoSacrifice(Entity<CultRuneComponent> rune, EntityUid victim, List<EntityUid> invokers)
    {
        _mind.TryGetMind(victim, out var mindId, out _);
        var isTarget = mindId != default && _rule.IsSacrificeTarget(mindId);
        var important = (HasComp<HumanoidProfileComponent>(victim) || HasComp<BorgChassisComponent>(victim)) && !_mobState.IsDead(victim) || isTarget;

        if (important && invokers.Count < 3)
        {
            foreach (var invoker in invokers)
                _cult.Message(invoker, Loc.GetString("cult-rune-offer-need-three", ("target", victim)));
            return false;
        }

        var rule = _rule.EnsureRule();
        rule.Comp.Sacrificed++;
        if (isTarget)
            _rule.SacrificeCompleted(mindId);

        _cult.Effect("CultEffectSacrifice", Transform(rune).Coordinates);

        foreach (var invoker in invokers)
        {
            var key = isTarget ? "cult-rune-offer-desired"
                : HasComp<HumanoidProfileComponent>(victim) || HasComp<BorgChassisComponent>(victim) ? "cult-rune-offer-accept" : "cult-rune-offer-meager";
            _cult.Message(invoker, Loc.GetString(key));
        }

        RaiseLocalEvent(new CultSacrificeCompletedEvent(victim, mindId == default ? null : mindId));

        if (HasComp<BorgChassisComponent>(victim))
        {
            var coords = Transform(rune).Coordinates;
            _constructs.OpenConstructChoice(invokers[0], type =>
            {
                if (TerminatingOrDeleted(victim))
                    return;
                _constructs.MakeConstruct(type, victim, invokers[0], coords, fromMind: victim);
                QueueDel(victim);
            });
            return true;
        }

        var stone = Spawn("CultSoulStone", Transform(rune).Coordinates);
        if (mindId != default)
            _constructs.CaptureSoul(stone, victim, invokers[0], forced: true);
        _audio.PlayPvs(CultSounds.Disintegrate, Transform(rune).Coordinates);
        _gibbing.Gib(victim);
        return true;
    }

    #endregion

    #region Телепорт

    private string AreaName(EntityUid uid)
    {
        return _navMap.GetNearestBeaconString(uid, onlyName: true);
    }

    private void InvokeTeleport(Entity<CultRuneComponent> rune, List<EntityUid> invokers)
    {
        var user = invokers[0];
        var options = new List<CultMenuOption>();
        var keys = new HashSet<string>();
        var query = EntityQueryEnumerator<CultRuneComponent>();
        while (query.MoveNext(out var uid, out var other))
        {
            if (uid == rune.Owner || other.RuneType != CultRuneType.Teleport)
                continue;
            var key = other.ListKey ?? AreaName(uid);
            var unique = key;
            var n = 1;
            while (!keys.Add(unique))
                unique = $"{key} ({++n})";
            options.Add(new CultMenuOption(GetNetEntity(uid).ToString(), unique));
        }

        if (options.Count == 0)
        {
            _cult.Message(user, Loc.GetString("cult-rune-teleport-none"));
            FailInvoke(rune);
            return;
        }

        _cult.OpenChoice(user, Loc.GetString("cult-rune-teleport-title"), options, id =>
        {
            if (TerminatingOrDeleted(rune) || !NetEntity.TryParse(id, out var net) || !TryGetEntity(net, out var dest)
                || !TryComp<CultRuneComponent>(dest, out var destRune))
            {
                if (!TerminatingOrDeleted(rune))
                    FailInvoke(rune);
                return;
            }
            if (!InRange(user, rune) || _blocker.CanInteract(user, null) == false)
            {
                FailInvoke(rune);
                return;
            }
            DoTeleport(rune, (dest.Value, destRune), user, invokers);
        }, radial: false);
    }

    private bool InRange(EntityUid user, EntityUid target)
    {
        var a = _transform.GetMapCoordinates(user);
        var b = _transform.GetMapCoordinates(target);
        return a.MapId == b.MapId && (a.Position - b.Position).Length() <= 1.6f;
    }

    private void DoTeleport(Entity<CultRuneComponent> rune, Entity<CultRuneComponent> dest, EntityUid user, List<EntityUid> invokers)
    {
        var target = Transform(dest).Coordinates;
        var tile = _turf.GetTileRef(target);
        if (tile == null || _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable))
        {
            _cult.Message(user, Loc.GetString("cult-rune-teleport-blocked"));
            FailInvoke(rune);
            return;
        }

        var origin = Transform(rune).Coordinates;
        var moveUserLater = false;
        var moved = false;
        var success = false;

        foreach (var ent in MovablesOnRune(rune))
        {
            if (HasComp<GhostComponent>(ent))
                continue;
            if (HasComp<HumanoidProfileComponent>(ent))
            {
                _cult.Effect("CultEffectPhaseOut", origin, Transform(ent).LocalRotation);
                _cult.Effect("CultEffectPhaseIn", target, Transform(ent).LocalRotation);
            }
            if (ent == user)
            {
                moveUserLater = true;
                moved = true;
                continue;
            }
            moved = true;
            if (CultTeleport(ent, target))
                success = true;
        }

        if (!moved)
        {
            FailInvoke(rune);
            return;
        }

        BaseInvoke(rune, invokers);
        _audio.PlayPvs(CultSounds.PortalEnter, origin, AudioParams.Default.WithVolume(CultSystem.Db(50)).WithVariation(0.05f));
        _audio.PlayPvs(CultSounds.PortalEnter, target, AudioParams.Default.WithVolume(CultSystem.Db(50)).WithVariation(0.05f));

        var fromSpace = IsSpaceAt(origin);
        var fromLava = HasComp<LavalandMapComponent>(_transform.GetMap(origin)) && !HasComp<LavalandMapComponent>(_transform.GetMap(target));
        var direction = (_transform.ToMapCoordinates(origin).Position - _transform.ToMapCoordinates(target).Position).GetDir();

        if (moveUserLater && CultTeleport(user, target))
            success = true;

        _cult.Message(user, Loc.GetString(success
            ? moveUserLater ? "cult-rune-teleport-self" : "cult-rune-teleport-sent"
            : moveUserLater ? "cult-rune-teleport-self-fail" : "cult-rune-teleport-sent-fail"));

        if (success)
            _popup.PopupCoordinates(Loc.GetString("cult-rune-teleport-crack"), origin, PopupType.MediumCaution);

        if (fromLava)
            HandlePortal(dest, "lava", direction);
        else if (fromSpace)
            HandlePortal(dest, "space", direction);

        if (success)
            _popup.PopupCoordinates(Loc.GetString("cult-rune-teleport-boom"), target, PopupType.MediumCaution);
    }

    private bool IsSpaceAt(EntityCoordinates coords)
    {
        var tile = _turf.GetTileRef(coords);
        return tile == null || _turf.IsSpace(tile.Value);
    }

    /// <summary>Всё незакреплённое на тайле руны.</summary>
    private List<EntityUid> MovablesOnRune(EntityUid rune)
    {
        var result = new List<EntityUid>();
        var xform = Transform(rune);
        foreach (var ent in _lookup.GetEntitiesInRange(xform.Coordinates, 0.45f, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (ent == rune)
                continue;
            var exf = Transform(ent);
            if (exf.Anchored || exf.ParentUid != xform.ParentUid)
                continue;
            if (HasComp<CultRuneComponent>(ent))
                continue;
            result.Add(ent);
        }
        return result;
    }

    /// <summary>do_teleport(channel = TELEPORT_CHANNEL_CULT): святая земля перехватывает.</summary>
    public bool CultTeleport(EntityUid ent, EntityCoordinates target)
    {
        if (_cult.IsBlessed(target))
            return false;
        Teleport(ent, target);
        return true;
    }

    public void Teleport(EntityUid ent, EntityCoordinates target)
    {
        if (TryComp<Content.Shared.Movement.Pulling.Components.PullableComponent>(ent, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(ent, pullable);
        _transform.SetCoordinates(ent, target);
        _transform.AttachToGridOrMap(ent);
    }

    /// <summary>teleport/handle_portal(): разрыв реальности на 60 секунд.</summary>
    private void HandlePortal(Entity<CultRuneComponent> rune, string type, Direction origin)
    {
        ClosePortal(rune);
        var coords = Transform(rune).Coordinates;
        _audio.PlayPvs(CultSounds.PortalCreated, coords, AudioParams.Default.WithMaxDistance(14));

        var inner = Spawn("CultEffectPortal", coords);
        _appearance.SetData(inner, CultVisuals.State, type);
        rune.Comp.Portal = inner;
        SpawnRuneSpawn(coords, "runeouter", 179, 60f, rune.Comp.RuneColor);

        Color lightColor;
        if (type == "space")
        {
            lightColor = rune.Comp.RuneColor;
            rune.Comp.PortalDescription = Loc.GetString("cult-rune-portal-space", ("dir", Loc.GetString($"zzzz-fmt-direction-{origin}")));
        }
        else
        {
            lightColor = Color.FromHex("#FA9632");
            rune.Comp.PortalDescription = Loc.GetString("cult-rune-portal-lava");
        }

        var light = _light.EnsureLight(rune);
        _light.SetColor(rune, lightColor, light);
        _light.SetRadius(rune, 4, light);
        _light.SetEnergy(rune, 4, light);
        _light.SetEnabled(rune, true, light);

        var target = rune.Owner;
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(60), () =>
        {
            if (!TerminatingOrDeleted(target) && TryComp<CultRuneComponent>(target, out var comp) && comp.Portal == inner)
                ClosePortal((target, comp));
        });
    }

    private void ClosePortal(Entity<CultRuneComponent> rune)
    {
        if (rune.Comp.Portal is { } portal && !TerminatingOrDeleted(portal))
            QueueDel(portal);
        rune.Comp.Portal = null;
        rune.Comp.PortalDescription = null;
        if (!TerminatingOrDeleted(rune))
            _light.SetEnabled(rune, false);
    }

    /// <summary>temp_visual/cult/rune_spawn.</summary>
    public EntityUid SpawnRuneSpawn(EntityCoordinates coords, string state, float turn, float duration, Color color)
    {
        var effect = Spawn(RuneSpawnEffect, coords);
        _appearance.SetData(effect, CultVisuals.State, state);
        _appearance.SetData(effect, CultVisuals.Color, color);
        var comp = EnsureComp<CultRuneSpawnEffectComponent>(effect);
        comp.Duration = duration;
        comp.Turn = turn;
        Dirty(effect, comp);
        EnsureComp<TimedDespawnComponent>(effect).Lifetime = duration;
        return effect;
    }

    /// <summary>Полный набор rune_spawn: внешнее кольцо, слова, внутреннее кольцо, центр.</summary>
    public List<EntityUid> SpawnRuneSpawnSet(EntityCoordinates coords, int pattern, float duration, Color color)
    {
        return new List<EntityUid>
        {
            SpawnRuneSpawn(coords, "runeouter", 179, duration, color),
            SpawnRuneSpawn(coords, $"rune{pattern}words", 181, duration, color),
            SpawnRuneSpawn(coords, $"rune{pattern}inner", 179, duration, color),
            SpawnRuneSpawn(coords, $"rune{pattern}center", 179, duration, color),
        };
    }

    #endregion

    #region Нар'Си

    private void InvokeNarSie(Entity<CultRuneComponent> rune, List<EntityUid> invokers)
    {
        if (rune.Comp.Used)
            return;
        if (_station.GetOwningStation(rune) == null)
            return;

        var user = invokers[0];
        var rule = _rule.EnsureRule();
        if (!_rule.IsInSummonSpot(rune))
        {
            _cult.Message(user, Loc.GetString("cult-rune-narsie-wrong-place", ("spots", _rule.SummonSpotsText(rule.Comp))));
            return;
        }

        if (EntityQuery<CultNarSieComponent>().Any())
        {
            foreach (var invoker in invokers)
                _cult.Message(invoker, Loc.GetString("cult-rune-narsie-already"));
            return;
        }

        rune.Comp.Used = true;
        rule.Comp.NarSieSummoned = true;
        BaseInvoke(rune, invokers);
        _audio.PlayGlobal(CultSounds.NarsieSummon, Filter.Broadcast(), true);
        foreach (var mind in rule.Comp.Members)
        {
            if (!rule.Comp.TrueCultists.Contains(mind))
                rule.Comp.TrueCultists.Add(mind);
        }

        var coords = _transform.GetMapCoordinates(rune);
        var runeUid = rune.Owner;
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(4), () =>
        {
            if (!TerminatingOrDeleted(runeUid))
                SetRuneColor((runeUid, rune.Comp), ColorRed);
            Spawn("CultNarSie", coords);
            RaiseLocalEvent(new CultNarSieSummonedEvent());
        });
    }

    #endregion

    #region Воскрешение

    private void InvokeRevive(Entity<CultRuneComponent> rune, List<EntityUid> invokers)
    {
        if (rune.Comp.InUse)
            return;
        rune.Comp.InUse = true;
        var user = invokers[0];

        var candidates = EntitiesOnRune(rune).Where(m => _cult.IsCultist(m) && (_mobState.IsDead(m) || !HasComp<ActorComponent>(m))).ToList();
        if (candidates.Count == 0)
        {
            _cult.Message(user, Loc.GetString("cult-rune-revive-none"));
            FailInvoke(rune);
            return;
        }

        if (candidates.Count > 1)
        {
            var options = candidates.Select(c => new CultMenuOption(GetNetEntity(c).ToString(), Name(c))).ToList();
            _cult.OpenChoice(user, Loc.GetString("cult-rune-revive-title"), options, id =>
            {
                if (NetEntity.TryParse(id, out var net) && TryGetEntity(net, out var picked))
                    DoRevive(rune, picked.Value, user, invokers);
                else
                    rune.Comp.InUse = false;
            }, radial: false);
            return;
        }

        DoRevive(rune, candidates[0], user, invokers);
    }

    private void DoRevive(Entity<CultRuneComponent> rune, EntityUid target, EntityUid user, List<EntityUid> invokers)
    {
        if (TerminatingOrDeleted(rune))
            return;
        if (!InRange(user, rune) || TerminatingOrDeleted(target))
        {
            FailInvoke(rune);
            return;
        }
        if (!OnRuneTile(rune, target))
        {
            _cult.Message(user, Loc.GetString("cult-rune-revive-moved"));
            FailInvoke(rune);
            return;
        }

        rune.Comp.Invocation = Name(user) == "Herbert West"
            ? "To life, to life, I bring them!"
            : "Pasnar val'keriam usinar. Savrae ines amutan. Yam'toth remium il'tarat!";

        if (_mobState.IsDead(target))
        {
            var rule = _rule.EnsureRule();
            var diff = rule.Comp.Sacrificed - rule.Comp.SoulsToRevive - rule.Comp.SacrificesUsed;
            if (diff < 0)
            {
                _cult.Message(user, Loc.GetString("cult-rune-revive-more-sacrifices", ("count", -diff)));
                FailInvoke(rune);
                return;
            }
            rule.Comp.SacrificesUsed += rule.Comp.SoulsToRevive;
            _rejuvenate.PerformRejuvenate(target);
        }

        // Душа возвращается в тело, если её владелец в игре; иначе тело отдаётся призракам.
        if (!HasComp<ActorComponent>(target))
        {
            if (_mind.TryGetMind(target, out var mindId, out var mind) && mind.UserId != null
                && _players.TryGetSessionById(mind.UserId, out var session) && session.AttachedEntity != target)
            {
                _mind.UnVisit(mindId, mind);
                if (mind.OwnedEntity != target)
                    _mind.TransferTo(mindId, target, mind: mind);
            }

            if (!HasComp<ActorComponent>(target))
            {
                var ghostRole = EnsureComp<GhostRoleComponent>(target);
                ghostRole.RoleName = Loc.GetString("cult-rune-revive-ghost-role", ("name", Name(target)));
                ghostRole.RoleDescription = Loc.GetString("cult-rune-revive-ghost-role-desc");
                EnsureComp<GhostTakeoverAvailableComponent>(target);
            }
        }

        _cult.SoundTo(target, CultSounds.BloodcultGain);
        _cult.Message(target, Loc.GetString("cult-rune-revive-arise"));
        _popup.PopupEntity(Loc.GetString("cult-rune-revive-breath", ("target", target)), target, Filter.PvsExcept(target), true, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("cult-rune-revive-alive"), target, target, PopupType.Large);
        rune.Comp.InUse = false;
        BaseInvoke(rune, invokers);
    }

    #endregion

    #region Барьер

    private void InvokeBarrier(Entity<CultRuneComponent> rune, List<EntityUid> invokers)
    {
        var user = invokers[0];
        BaseInvoke(rune, invokers);
        if (rune.Comp.Barrier == null || TerminatingOrDeleted(rune.Comp.Barrier.Value))
        {
            var barrier = Spawn("CultRuneBarrierShield", Transform(rune).Coordinates);
            rune.Comp.Barrier = barrier;
            EnsureComp<CultBarrierComponent>(barrier).Rune = rune;
            SetBarrier(barrier, false);
        }
        ToggleBarrier(rune.Comp.Barrier.Value);
        if (HasComp<HumanoidProfileComponent>(user))
            _cult.Damage(user, "Slash", 2);
    }

    /// <summary>create_rune/wall: руна барьера, созданная конструктом, сразу активна.</summary>
    public void ToggleBarrierOf(EntityUid rune)
    {
        if (!TryComp<CultRuneComponent>(rune, out var comp))
            return;
        var barrier = Spawn("CultRuneBarrierShield", Transform(rune).Coordinates);
        comp.Barrier = barrier;
        EnsureComp<CultBarrierComponent>(barrier).Rune = rune;
        SetBarrier(barrier, true);
    }

    /// <summary>Активировать барьер руны: призыв этим барьером.</summary>
    public void ToggleBarrier(EntityUid barrier)
    {
        if (!TryComp<CultBarrierComponent>(barrier, out var comp))
            return;
        SetBarrier(barrier, !comp.Active);
    }

    public void SetBarrier(EntityUid barrier, bool active)
    {
        if (!TryComp<CultBarrierComponent>(barrier, out var comp))
            return;
        comp.Active = active;
        if (TryComp<PhysicsComponent>(barrier, out var physics))
            _physics.SetCanCollide(barrier, active, body: physics);
        var vis = EnsureComp<VisibilityComponent>(barrier);
        _visibility.SetLayer((barrier, vis), (ushort) (active ? VisibilityFlags.Normal : VisibilityFlags.Ghost));
        _appearance.SetData(barrier, CultVisuals.Concealed, !active);
    }

    private void OnBarrierActivate(Entity<CultBarrierComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || ent.Comp.Rune is not { } rune || !TryComp<CultRuneComponent>(rune, out var comp))
            return;
        args.Handled = true;
        if (!_cult.IsCultAligned(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cult-rune-cant-understand", ("rune", rune)), args.User, args.User);
            return;
        }
        TryInvoke((rune, comp), args.User);
    }

    private void OnBarrierShutdown(Entity<CultBarrierComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Rune is not { } rune || TerminatingOrDeleted(rune))
            return;
        _popup.PopupEntity(Loc.GetString("cult-rune-barrier-destroyed", ("rune", rune)), rune, PopupType.MediumCaution);
        QueueDel(rune);
    }

    #endregion

    #region Призыв культиста

    private void InvokeSummon(Entity<CultRuneComponent> rune, List<EntityUid> invokers)
    {
        var user = invokers[0];
        var cultists = _cult.CultistBodies().Where(c => !invokers.Contains(c) && !_mobState.IsDead(c)).ToList();
        if (cultists.Count == 0)
        {
            _cult.Message(user, Loc.GetString("cult-rune-summon-none"));
            FailInvoke(rune);
            return;
        }

        var options = cultists.Select(c => new CultMenuOption(GetNetEntity(c).ToString(), Name(c))).ToList();
        _cult.OpenChoice(user, Loc.GetString("cult-rune-summon-title"), options, id =>
        {
            if (TerminatingOrDeleted(rune) || !InRange(user, rune) || !_blocker.CanInteract(user, null))
                return;
            if (!NetEntity.TryParse(id, out var net) || !TryGetEntity(net, out var picked))
            {
                _cult.Message(user, Loc.GetString("cult-rune-summon-no-target"));
                FailInvoke(rune);
                return;
            }
            var target = picked.Value;
            if (_mobState.IsDead(target))
            {
                _cult.Message(user, Loc.GetString("cult-rune-summon-died", ("target", target)));
                FailInvoke(rune);
                return;
            }
            if (_pulling.IsPulled(target) || _buckle.IsBuckled(target))
            {
                _cult.Message(user, Loc.GetString("cult-rune-summon-held", ("target", target)));
                FailInvoke(rune);
                return;
            }
            if (!_cult.IsCultist(target))
            {
                _cult.Message(user, Loc.GetString("cult-rune-summon-not-cultist", ("target", target)));
                FailInvoke(rune);
                return;
            }

            _popup.PopupEntity(Loc.GetString("cult-rune-summon-vanish", ("target", target)), target, Filter.PvsExcept(target), true, PopupType.MediumCaution);
            _cult.Message(target, Loc.GetString("cult-rune-summon-vertigo"));
            BaseInvoke(rune, CanInvoke(rune, user));
            _popup.PopupEntity(Loc.GetString("cult-rune-summon-appear", ("rune", rune), ("target", target)), rune, PopupType.MediumCaution);

            var old = Transform(target).Coordinates;
            Teleport(target, Transform(rune).Coordinates);
            _audio.PlayPvs(CultSounds.PortalEnter, Transform(rune).Coordinates, AudioParams.Default.WithVariation(0.05f));
            _audio.PlayPvs(CultSounds.PortalEnter, old, AudioParams.Default.WithVariation(0.05f));
            QueueDel(rune);
        }, radial: false);
    }

    #endregion

    #region Кипение крови

    private void InvokeBloodBoil(Entity<CultRuneComponent> rune, List<EntityUid> invokers)
    {
        if (rune.Comp.InUse)
            return;
        BaseInvoke(rune, invokers, glow: false);
        rune.Comp.InUse = true;

        _popup.PopupEntity(Loc.GetString("cult-rune-boil-glow", ("rune", rune)), rune, PopupType.MediumCaution);
        var color = Color.FromHex("#FC9B54");
        SetRuneColor(rune, color);
        SetBoilLight(rune, color);

        foreach (var target in BoilTargets(rune))
            _cult.Message(target, Loc.GetString("cult-rune-boil-veins"));

        var uid = rune.Owner;
        SetRuneColor(rune, Color.FromHex("#FCB56D"));
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(0.4), () =>
        {
            if (TerminatingOrDeleted(uid))
                return;
            DoAreaBurn(rune, 0.5f);
            SetRuneColor(rune, Color.FromHex("#FFDF80"));
            Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(0.5), () =>
            {
                if (TerminatingOrDeleted(uid))
                    return;
                DoAreaBurn(rune, 1f);
                SetRuneColor(rune, Color.FromHex("#FFFDF4"));
                Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(0.6), () =>
                {
                    if (TerminatingOrDeleted(uid))
                        return;
                    DoAreaBurn(rune, 1.5f);
                    Hotspot(Transform(uid).Coordinates);
                    QueueDel(uid);
                });
            });
        });
    }

    private void SetBoilLight(EntityUid rune, Color color)
    {
        var light = _light.EnsureLight(rune);
        _light.SetColor(rune, color, light);
        _light.SetRadius(rune, 6, light);
        _light.SetEnergy(rune, 1, light);
        _light.SetEnabled(rune, true, light);
    }

    /// <summary>viewers(T) — не культисты с кровью и без защиты от магии.</summary>
    private List<EntityUid> BoilTargets(EntityUid rune)
    {
        var result = new List<EntityUid>();
        var origin = _transform.GetMapCoordinates(rune);
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(origin, 7f))
        {
            if (_cult.IsCultist(mob) || !HasComp<Content.Shared.Body.Components.BloodstreamComponent>(mob))
                continue;
            if (!_examine.InRangeUnOccluded(origin, _transform.GetMapCoordinates(mob), 7f, null))
                continue;
            if (_cult.BlocksMagic(mob))
                continue;
            result.Add(mob);
        }
        return result;
    }

    private void DoAreaBurn(Entity<CultRuneComponent> rune, float multiplier)
    {
        SetBoilLight(rune, Color.FromHex("#FC9B54"));
        const float tick = 25f;
        foreach (var target in BoilTargets(rune))
            _cult.OverallDamage(target, tick * multiplier, tick * multiplier, rune);
    }

    public void Hotspot(EntityCoordinates coords)
    {
        var grid = _transform.GetGrid(coords);
        if (grid == null || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;
        var tile = _map.TileIndicesFor(grid.Value, gridComp, coords);
        _atmos.HotspotExpose(grid.Value, tile, 700f, 50f, null, true);
    }

    #endregion

    #region Царство духов

    private void InvokeSpiritRealm(Entity<CultRuneComponent> rune, List<EntityUid> invokers)
    {
        BaseInvoke(rune, invokers);
        var user = invokers[0];

        _cult.OpenChoice(user, Loc.GetString("cult-rune-manifest-title"), new List<CultMenuOption>
        {
            new("ghost", Loc.GetString("cult-rune-manifest-summon")),
            new("ascend", Loc.GetString("cult-rune-manifest-ascend")),
        }, choice =>
        {
            if (TerminatingOrDeleted(rune) || TerminatingOrDeleted(user))
                return;
            if (choice == "ghost")
                SummonCultGhost(rune, user);
            else if (choice == "ascend")
                AscendDarkSpirit(rune, user);
        }, radial: false);
    }

    private void SummonCultGhost(Entity<CultRuneComponent> rune, EntityUid user)
    {
        var coords = Transform(rune).Coordinates;
        if (_station.GetOwningStation(rune) == null)
        {
            _cult.Message(user, Loc.GetString("cult-rune-manifest-not-station"));
            return;
        }
        if (rune.Comp.Ghosts >= GhostLimit)
        {
            _cult.Message(user, Loc.GetString("cult-rune-manifest-too-many"));
            FailInvoke(rune);
            return;
        }

        // notify_ghosts.
        var ghostFilter = Filter.Empty().AddWhereAttachedEntity(HasComp<GhostComponent>);
        _cult.MessageGhosts(ghostFilter, Loc.GetString("cult-rune-manifest-notify", ("area", AreaName(rune))));
        _audio.PlayGlobal(CultSounds.Ghost2, ghostFilter, true);

        var ghosts = new List<EntityUid>();
        foreach (var ghost in _lookup.GetEntitiesInRange<GhostComponent>(coords, 0.6f))
        {
            if (HasComp<ActorComponent>(ghost) && _mind.TryGetMind(ghost, out _, out _))
                ghosts.Add(ghost.Owner);
        }

        if (ghosts.Count == 0)
        {
            _cult.Message(user, Loc.GetString("cult-rune-manifest-no-spirits", ("rune", rune)));
            FailInvoke(rune);
            return;
        }

        var chosen = _random.Pick(ghosts);
        if (!_mind.TryGetMind(chosen, out var mindId, out var mind))
            return;

        var human = Spawn("MobCultGhost", coords);
        _cult.SetName(human, mind.CharacterName ?? Name(chosen));
        _spawning.EquipStartingGear(human, "CultGhostGear");
        _eye.SetVisibilityMask(human, (int) (VisibilityFlags.Normal | VisibilityFlags.Ghost));
        rune.Comp.Ghosts++;

        _audio.PlayPvs(CultSounds.ExitBlood, coords, AudioParams.Default.WithVolume(CultSystem.Db(50)).WithVariation(0.05f));
        _popup.PopupEntity(Loc.GetString("cult-rune-manifest-mist", ("rune", rune)), rune, PopupType.MediumCaution);
        _cult.Message(user, Loc.GetString("cult-rune-manifest-flowing", ("rune", rune)));

        var shield = Spawn("CultShieldWeak", coords);
        var spirit = EnsureComp<CultSpiritGhostComponent>(human);
        spirit.Rune = rune;
        spirit.Invoker = user;
        spirit.Shield = shield;
        spirit.NextDrain = _timing.CurTime + TimeSpan.FromSeconds(0.1);
        spirit.OriginalMind = mindId;

        _mind.TransferTo(mindId, human, mind: mind);
        _cult.AddCultist(human, silent: true, cultGhost: true);
        _cult.Message(human, Loc.GetString("cult-rune-manifest-servant"));
    }

    private void OnSpiritGhostState(Entity<CultSpiritGhostComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            DissolveCultGhost(ent);
    }

    private void DissolveCultGhost(Entity<CultSpiritGhostComponent> ent)
    {
        if (ent.Comp.Dissolving)
            return;
        ent.Comp.Dissolving = true;

        if (ent.Comp.Shield is { } shield && !TerminatingOrDeleted(shield))
            QueueDel(shield);
        if (ent.Comp.Rune is { } rune && TryComp<CultRuneComponent>(rune, out var runeComp))
            runeComp.Ghosts = Math.Max(0, runeComp.Ghosts - 1);

        _popup.PopupEntity(Loc.GetString("cult-rune-manifest-dissolve", ("target", ent)), ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
        _cult.Message(ent, Loc.GetString("cult-rune-manifest-fades"));
        _cult.RemoveCultist(ent);
        _structures.DustBody(ent);
    }

    private void AscendDarkSpirit(Entity<CultRuneComponent> rune, EntityUid user)
    {
        if (!_mind.TryGetMind(user, out var mindId, out var mind))
            return;

        EnsureComp<CultTintComponent>(user).Color = ColorDarkRed;
        Dirty(user, Comp<CultTintComponent>(user));
        _popup.PopupEntity(Loc.GetString("cult-rune-ascend-freeze", ("target", user)), user, Filter.PvsExcept(user), true, PopupType.MediumCaution);
        _cult.Message(user, Loc.GetString("cult-rune-ascend-self"));

        var ghost = _ghost.SpawnGhost((mindId, mind), user, canReturn: true);
        if (ghost == null)
            return;

        _cult.SetName(ghost.Value, Loc.GetString("cult-rune-ascend-name", ("name", Name(ghost.Value))));
        var tint = EnsureComp<CultTintComponent>(ghost.Value);
        tint.Color = Color.Red;
        Dirty(ghost.Value, tint);

        var spirit = EnsureComp<CultDarkSpiritComponent>(ghost.Value);
        spirit.Body = user;
        spirit.Rune = rune;
        spirit.NextCheck = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        _cult.GrantDarkSpiritActions(ghost.Value, spirit);

        var body = EnsureComp<CultSpiritBodyComponent>(user);
        body.Ghost = ghost;
        body.Rune = rune;
        rune.Comp.InUse = true;
    }

    private void UpdateDarkSpirits(TimeSpan now)
    {
        var query = EntityQueryEnumerator<CultSpiritBodyComponent>();
        var ended = new List<EntityUid>();
        while (query.MoveNext(out var uid, out var body))
        {
            if (body.Ghost is not { } ghost || !TryComp<CultDarkSpiritComponent>(ghost, out var spirit) || now < spirit.NextCheck)
            {
                if (body.Ghost == null || TerminatingOrDeleted(body.Ghost.Value))
                    ended.Add(uid);
                continue;
            }
            spirit.NextCheck = now + TimeSpan.FromSeconds(0.5);

            if (body.Rune is { } rune && !TerminatingOrDeleted(rune) && !OnRuneTile(rune, uid))
            {
                _popup.PopupEntity(Loc.GetString("cult-rune-ascend-pulled", ("target", uid)), uid, PopupType.MediumCaution);
                _cult.Beam(uid, Transform(rune).Coordinates, "CultBeamDrain", 0.2f);
                Teleport(uid, Transform(rune).Coordinates);
            }

            if (HasComp<ActorComponent>(uid))
            {
                _popup.PopupEntity(Loc.GetString("cult-rune-ascend-relax", ("target", uid)), uid, Filter.PvsExcept(uid), true, PopupType.Medium);
                _cult.Message(uid, Loc.GetString("cult-rune-ascend-reunited", ("rune", body.Rune ?? uid)));
                _cult.Paralyze(uid, 4);
                ended.Add(uid);
                continue;
            }

            if (TryComp<DamageableComponent>(uid, out var damageable) && _cult.HealthOf(uid, damageable) <= 10)
            {
                _cult.Message(ghost, Loc.GetString("cult-rune-ascend-cant-sustain"));
                ended.Add(uid);
            }
        }

        foreach (var uid in ended)
            EndDarkSpirit(uid);
    }

    private void EndDarkSpirit(EntityUid bodyUid)
    {
        if (!TryComp<CultSpiritBodyComponent>(bodyUid, out var body))
            return;
        if (body.Ghost is { } ghost && TryComp<CultDarkSpiritComponent>(ghost, out var spirit))
        {
            _cult.RemoveDarkSpiritActions(ghost, spirit);
            RemComp<CultTintComponent>(ghost);
        }
        if (body.Rune is { } rune && TryComp<CultRuneComponent>(rune, out var runeComp))
            runeComp.InUse = false;
        RemComp<CultTintComponent>(bodyUid);

        // grab_ghost(): вернуть душу в тело.
        if (_mind.TryGetMind(bodyUid, out var mindId, out var mind) && mind.VisitingEntity != null)
            _mind.UnVisit(mindId, mind);
        RemComp<CultSpiritBodyComponent>(bodyUid);
    }

    #endregion

    #region Апокалипсис

    private void InvokeApocalypse(Entity<CultRuneComponent> rune, List<EntityUid> invokers)
    {
        if (rune.Comp.InUse)
            return;
        BaseInvoke(rune, invokers);

        var user = invokers[0];
        var rule = _rule.EnsureRule();
        if (rule.Comp.SummonSpots.Count <= 1)
        {
            _cult.Message(user, Loc.GetString("cult-rune-apoc-last-site"));
            return;
        }
        if (!_rule.IsInSummonSpot(rune))
        {
            _cult.Message(user, Loc.GetString("cult-rune-apoc-wrong-place", ("spots", _rule.SummonSpotsText(rule.Comp))));
            return;
        }

        var place = AreaName(rune);
        _rule.RemoveSummonSpot(rune);
        rune.Comp.InUse = true;

        var coords = Transform(rune).Coordinates;
        _cult.Effect("CultEffectGraspPortal", coords);

        var cultists = 0;
        var players = 0;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } mob || !HasComp<MobStateComponent>(mob))
                continue;
            players++;
            if (_cult.IsCultist(mob))
                cultists++;
        }
        var ratio = players == 0 ? 0f : (float) cultists / players;
        var intensity = MathF.Max(60f, 360f - 360f * MathF.Pow(ratio + 0.3f, 2));
        var duration = TimeSpan.FromSeconds(intensity);

        _audio.PlayPvs(CultSounds.EnterBlood, coords, AudioParams.Default.WithVariation(0.05f));
        _popup.PopupCoordinates(Loc.GetString("cult-rune-apoc-shockwave"), coords, PopupType.LargeCaution);

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 3f))
            _cult.Paralyze(mob, 3);
        _emp.EmpPulse(_transform.ToMapCoordinates(coords), 0.42f * intensity, 50000f, TimeSpan.FromSeconds(intensity / 10f));

        var mapId = _transform.GetMapId(coords);
        var end = _timing.CurTime + duration;
        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var mob, out _, out var xform))
        {
            if (xform.MapID != mapId || _mobState.IsDead(mob))
                continue;
            var vision = EnsureComp<CultApocalypseVisionComponent>(mob);
            vision.End = end;
            vision.Construct = HasComp<HumanoidProfileComponent>(mob) ? null : _random.Pick(new[] { "wraith", "artificer", "juggernaut" });
            Dirty(mob, vision);

            if (HasComp<HumanoidProfileComponent>(mob) && HasComp<ActorComponent>(mob))
            {
                _cult.SoundTo(mob, _random.Pick(new[] { CultSounds.BloodcultGain, CultSounds.GhostWhisper, CultSounds.GhostyWind }));
            }
            if (_cult.IsCultist(mob))
            {
                _cult.Message(mob, Loc.GetString("cult-rune-apoc-announce", ("area", place)));
                _cult.SoundTo(mob, CultSounds.PopeEntry);
            }
        }

        if (intensity >= 285)
            ApocalypseEvent();

        QueueDel(rune);
    }

    /// <summary>Побочные эффекты для слабого культа.</summary>
    private void ApocalypseEvent()
    {
        var outcome = _random.Next(1, 101);
        string[] rules = outcome switch
        {
            <= 10 => new[] { "MouseMigration" },
            <= 20 => new[] { "SolarFlare" },
            <= 30 => new[] { "VentClog" },
            <= 40 => new[] { "ImmovableRodSpawn", "ImmovableRodSpawn", "ImmovableRodSpawn" },
            <= 50 => new[] { "MeteorSwarm" },
            <= 60 => new[] { "SpiderSpawnHorde" },
            <= 70 => new[] { "AnomalySpawn", "AnomalySpawn", "AnomalySpawn", "AnomalySpawn" },
            <= 80 => new[] { "KudzuGrowth", "BureaucraticError" },
            _ => Array.Empty<string>(),
        };

        if (outcome > 80)
        {
            EntityManager.System<CultPortalStormSystem>().StartStorm();
            return;
        }

        foreach (var id in rules)
        {
            if (_proto.HasIndex<EntityPrototype>(id))
                _ticker.StartGameRule(id);
        }
    }

    #endregion

    #region Ритуальный предмет

    private void OnRitualExamined(Entity<CultRitualItemComponent> ent, ref ExaminedEvent args)
    {
        if (_cult.IsCultist(args.Examiner) && ent.Comp.ExamineMessage != null)
            args.PushMarkup(Loc.GetString(ent.Comp.ExamineMessage.Value));
    }

    private List<(EntProtoId Id, CultRuneComponent Rune, string Name)> Scribable()
    {
        if (_scribable != null)
            return _scribable;
        _scribable = new();
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.TryGetComponent<CultRuneComponent>(out var rune, _factory) || !rune.CanBeScribed)
                continue;
            _scribable.Add((proto.ID, rune, Loc.GetString(rune.CultistName)));
        }
        _scribable.Sort((a, b) => a.Rune.ScribeOrder.CompareTo(b.Rune.ScribeOrder));
        return _scribable;
    }

    private void OnRitualUse(Entity<CultRitualItemComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        var user = args.User;
        if (!CanScribe(ent, user))
            return;
        args.Handled = true;

        if (ent.Comp.Drawing)
        {
            _popup.PopupEntity(Loc.GetString("cult-scribe-already"), user, user);
            return;
        }

        StartScribe(ent, user);
    }

    private void StartScribe(Entity<CultRitualItemComponent> ent, EntityUid user)
    {
        var options = Scribable().Select(r => new CultMenuOption(r.Id, r.Name)).ToList();
        _cult.OpenChoice(user, Loc.GetString("cult-scribe-title"), options, id =>
        {
            if (!CanScribe(ent, user))
                return;
            var entry = Scribable().FirstOrDefault(r => r.Id == id);
            if (entry.Rune == null)
                return;

            if (entry.Rune.ReqKeyword)
            {
                _cult.OpenText(user, Loc.GetString("cult-scribe-keyword-title"), Loc.GetString("cult-scribe-keyword-prompt"), 32, keyword =>
                {
                    ContinueScribe(ent, user, entry.Id, entry.Rune, keyword);
                });
                return;
            }

            ContinueScribe(ent, user, entry.Id, entry.Rune, null);
        }, radial: false);
    }

    private void ContinueScribe(Entity<CultRitualItemComponent> ent, EntityUid user, EntProtoId proto, CultRuneComponent rune, string? keyword)
    {
        if (!CanScribe(ent, user))
            return;

        var xform = Transform(user);
        if (rune.RuneType == CultRuneType.Summon && (_station.GetOwningStation(user) == null || IsSpaceAt(xform.Coordinates)))
        {
            _cult.Message(user, Loc.GetString("cult-scribe-summon-station"));
            return;
        }

        if (rune.RuneType == CultRuneType.Apocalypse)
        {
            var elapsed = _ticker.RoundDuration();
            if (elapsed <= TimeSpan.FromMinutes(10))
            {
                var wait = TimeSpan.FromMinutes(10) - elapsed;
                _cult.Message(user, Loc.GetString("cult-scribe-apoc-wait", ("time", $"{(int) wait.TotalMinutes}:{wait.Seconds:00}")));
                return;
            }
            if (!CheckRitualSite(user, failIfLast: true))
                return;
        }

        if (rune.RuneType == CultRuneType.NarSie)
        {
            ScribeNarSie(ent, user, proto, rune, keyword);
            return;
        }

        BeginScribe(ent, user, proto, rune, keyword);
    }

    private bool CheckRitualSite(EntityUid user, bool failIfLast = false)
    {
        var rule = _rule.EnsureRule();
        if (rule.Comp.SummonSpots.Count == 0)
        {
            _cult.Message(user, Loc.GetString("cult-scribe-no-sites"));
            return false;
        }
        if (!_rule.IsInSummonSpot(user))
        {
            _cult.Message(user, Loc.GetString("cult-scribe-wrong-site", ("spots", _rule.SummonSpotsText(rule.Comp))));
            return false;
        }
        if (failIfLast && rule.Comp.SummonSpots.Count <= 1)
        {
            _cult.Message(user, Loc.GetString("cult-scribe-last-site"));
            return false;
        }
        return true;
    }

    /// <summary>scribe_narsie_rune().</summary>
    private void ScribeNarSie(Entity<CultRitualItemComponent> ent, EntityUid user, EntProtoId proto, CultRuneComponent rune, string? keyword)
    {
        if (!CheckRitualSite(user))
            return;
        var rule = _rule.EnsureRule();
        if (!rule.Comp.SacrificeDone)
        {
            _cult.Message(user, Loc.GetString("cult-scribe-narsie-sacrifice"));
            return;
        }
        if (rule.Comp.NarSieSummoned)
        {
            _cult.Message(user, Loc.GetString("cult-scribe-narsie-already"));
            return;
        }

        _cult.OpenChoice(user, Loc.GetString("cult-scribe-narsie-confirm-title"), new List<CultMenuOption>
        {
            new("yes", Loc.GetString("cult-scribe-narsie-confirm-yes")),
            new("no", Loc.GetString("cult-scribe-narsie-confirm-no")),
        }, choice =>
        {
            if (choice != "yes")
            {
                _cult.Message(user, Loc.GetString("cult-scribe-narsie-decline"));
                return;
            }
            if (!CanScribe(ent, user) || !CheckRitualSite(user))
                return;

            var area = AreaName(user);
            _cult.Announce(Loc.GetString("cult-narsie-scribe-announcement", ("name", Name(user)), ("area", area)),
                rule.Comp.SummonAnnounced ? CultSounds.Notice3 : CultSounds.CultSummonAnnounce);
            rule.Comp.SummonAnnounced = true;

            foreach (var coords in _cult.FreeTurfsAround(user, includeBlocked: true))
                ent.Comp.Shields.Add(Spawn("CultShieldNarSie", coords));

            var ghostFilter = Filter.Empty().AddWhereAttachedEntity(HasComp<GhostComponent>);
            _cult.MessageGhosts(ghostFilter, Loc.GetString("cult-narsie-scribe-ghosts", ("name", Name(user))));

            _cult.NarSieSummonStarted();
            BeginScribe(ent, user, proto, rune, keyword);
        }, radial: false);
    }

    private void BeginScribe(Entity<CultRitualItemComponent> ent, EntityUid user, EntProtoId proto, CultRuneComponent rune, string? keyword)
    {
        var bloody = HasComp<Content.Shared.Body.Components.BloodstreamComponent>(user);
        _popup.PopupEntity(Loc.GetString(bloody ? "cult-scribe-start-others-blood" : "cult-scribe-start-others", ("user", user)),
            user, Filter.PvsExcept(user), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString(bloody ? "cult-scribe-start-self-blood" : "cult-scribe-start-self"), user, user);

        if (bloody)
            _cult.Damage(user, "Slash", rune.ScribeDamage);

        var delay = rune.ScribeDelay;
        if (!rune.NoScribeBoost && _structures.IsCultFloor(Transform(user).Coordinates))
            delay *= 0.5;

        _cult.SoundTo(user, CultSounds.Slice, CultSystem.Db(10));
        ent.Comp.Drawing = true;

        var doAfter = new DoAfterArgs(EntityManager, user, delay, new CultScribeRuneDoAfterEvent { Rune = proto, Keyword = keyword }, user, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = true,
            BreakOnHandChange = true,
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            ent.Comp.Drawing = false;
            CleanupShields(ent);
            if (rune.RuneType == CultRuneType.NarSie)
                _cult.NarSieSummonFailed();
        }
    }

    private void OnScribeDoAfter(Entity<CultistComponent> ent, ref CultScribeRuneDoAfterEvent args)
    {
        if (args.Used is not { } used || !TryComp<CultRitualItemComponent>(used, out var ritual))
            return;
        ritual.Drawing = false;
        var item = (used, ritual);

        var isNarSie = _proto.TryIndex<EntityPrototype>(args.Rune, out var proto)
                       && proto.TryGetComponent<CultRuneComponent>(out var runeComp, _factory)
                       && runeComp.RuneType == CultRuneType.NarSie;

        if (args.Cancelled || args.Handled || !CanScribe(item, ent))
        {
            CleanupShields(item);
            if (isNarSie)
                _cult.NarSieSummonFailed();
            return;
        }
        args.Handled = true;

        _popup.PopupEntity(Loc.GetString(HasComp<Content.Shared.Body.Components.BloodstreamComponent>(ent) ? "cult-scribe-done-others-blood" : "cult-scribe-done-others", ("user", ent)),
            ent, Filter.PvsExcept(ent), true, PopupType.Medium);
        _cult.Message(ent, Loc.GetString("cult-scribe-done-self"));
        CleanupShields(item);

        var rune = CreateRune(args.Rune, Transform(ent).Coordinates, args.Keyword);
        if (TryComp<CultRuneComponent>(rune, out var made))
            _cult.Message(ent, Loc.GetString("cult-scribe-made", ("name", Loc.GetString(made.CultistName).ToLowerInvariant()), ("desc", Loc.GetString(made.CultistDesc))));
    }

    /// <summary>new rune_to_scribe(turf, keyword).</summary>
    public EntityUid CreateRune(EntProtoId proto, EntityCoordinates coords, string? keyword)
    {
        var grid = _transform.GetGrid(coords);
        if (grid != null && TryComp<MapGridComponent>(grid, out var gridComp))
            coords = _map.GridTileToLocal(grid.Value, gridComp, _map.TileIndicesFor(grid.Value, gridComp, coords));

        var rune = Spawn(proto, coords);
        if (TryComp<CultRuneComponent>(rune, out var comp))
        {
            comp.Keyword = keyword;
            var area = AreaName(rune);
            comp.ListKey = string.IsNullOrEmpty(keyword) ? area : $"{keyword} {area}";
        }
        return rune;
    }

    private void CleanupShields(Entity<CultRitualItemComponent> ent)
    {
        foreach (var shield in ent.Comp.Shields)
        {
            if (!TerminatingOrDeleted(shield))
                QueueDel(shield);
        }
        ent.Comp.Shields.Clear();
    }

    /// <summary>can_scribe_rune() + check_rune_turf().</summary>
    private bool CanScribe(Entity<CultRitualItemComponent> ent, EntityUid user)
    {
        if (!_cult.IsCultist(user))
        {
            _popup.PopupEntity(Loc.GetString("cult-scribe-unintelligible", ("item", ent)), user, user);
            return false;
        }
        if (TerminatingOrDeleted(ent) || !_hands.IsHolding(user, ent))
            return false;
        if (!_blocker.CanInteract(user, null) || _mobState.IsIncapacitated(user))
        {
            _popup.PopupEntity(Loc.GetString("cult-scribe-cant-now"), user, user);
            return false;
        }

        var coords = Transform(user).Coordinates;
        if (IsSpaceAt(coords))
        {
            _popup.PopupEntity(Loc.GetString("cult-scribe-space"), user, user);
            return false;
        }
        if (RuneAt(coords) != null)
        {
            _popup.PopupEntity(Loc.GetString("cult-scribe-rune-exists"), user, user);
            return false;
        }
        if (!VeilWeak(user))
        {
            _popup.PopupEntity(Loc.GetString("cult-scribe-veil"), user, user);
            return false;
        }
        return true;
    }

    /// <summary>is_station_level() || is_mining_level(): станция или лаваленд.</summary>
    public bool VeilWeak(EntityUid uid)
    {
        if (_station.GetOwningStation(uid) != null)
            return true;
        var map = Transform(uid).MapUid;
        return map != null && HasComp<LavalandMapComponent>(map);
    }

    public EntityUid? RuneAt(EntityCoordinates coords)
    {
        foreach (var rune in _lookup.GetEntitiesInRange<CultRuneComponent>(coords, 0.45f))
            return rune.Owner;
        return null;
    }

    private void OnRitualAfterInteract(Entity<CultRitualItemComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !_cult.IsCultist(args.User))
            return;

        if (TryComp<CultRuneComponent>(target, out var rune))
        {
            args.Handled = true;
            TryScrapeRune(ent, (target, rune), args.User);
            return;
        }

        if (HasComp<CultBarrierComponent>(target) && Comp<CultBarrierComponent>(target).Rune is { } barrierRune
            && TryComp<CultRuneComponent>(barrierRune, out var barrierComp))
        {
            args.Handled = true;
            TryScrapeRune(ent, (barrierRune, barrierComp), args.User);
            return;
        }

        if (_structures.TryRitualHit(target, args.User, ent))
        {
            args.Handled = true;
            return;
        }

        if (_cult.IsCultist(target) && _magic.TryPurgeHolyWater(target, args.User, ent))
            args.Handled = true;
    }

    private void TryScrapeRune(Entity<CultRitualItemComponent> item, Entity<CultRuneComponent> rune, EntityUid user)
    {
        if (rune.Comp.LogWhenErased)
        {
            var name = Loc.GetString(rune.Comp.CultistName);
            _cult.OpenChoice(user, Loc.GetString("cult-erase-confirm-title", ("name", name)), new List<CultMenuOption>
            {
                new("yes", Loc.GetString("cult-erase-confirm-yes")),
                new("no", Loc.GetString("cult-erase-confirm-no")),
            }, choice =>
            {
                if (choice == "yes" && CanScrape(item, rune, user))
                    StartScrape(item, rune, user);
            }, radial: false);
            return;
        }

        StartScrape(item, rune, user);
    }

    private void StartScrape(Entity<CultRitualItemComponent> item, Entity<CultRuneComponent> rune, EntityUid user)
    {
        _cult.SoundTo(user, CultSounds.Sheath);
        var doAfter = new DoAfterArgs(EntityManager, user, rune.Comp.EraseTime, new CultEraseRuneDoAfterEvent(), user, target: rune, used: item)
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnHandChange = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private bool CanScrape(Entity<CultRitualItemComponent> item, Entity<CultRuneComponent> rune, EntityUid user)
    {
        return _cult.IsCultist(user) && _hands.IsHolding(user, item) && InRange(user, rune)
               && !_mobState.IsIncapacitated(user) && _blocker.CanInteract(user, null);
    }

    private void OnEraseDoAfter(Entity<CultistComponent> ent, ref CultEraseRuneDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || args.Used is not { } used
            || !TryComp<CultRuneComponent>(target, out var rune) || !TryComp<CultRitualItemComponent>(used, out var item))
            return;
        if (!CanScrape((used, item), (target, rune), ent))
            return;
        args.Handled = true;
        _cult.Message(ent, Loc.GetString("cult-erase-done", ("name", Loc.GetString(rune.CultistName).ToLowerInvariant())));
        QueueDel(target);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        // Поддержание призраков культа: 0.1 brute каждые 0.1 с призывающему.
        var ghosts = EntityQueryEnumerator<CultSpiritGhostComponent>();
        var dissolve = new List<EntityUid>();
        while (ghosts.MoveNext(out var uid, out var ghost))
        {
            if (ghost.Dissolving || now < ghost.NextDrain)
                continue;
            ghost.NextDrain = now + TimeSpan.FromSeconds(0.1);

            if (ghost.Rune is not { } rune || TerminatingOrDeleted(rune) || ghost.Invoker is not { } invoker || TerminatingOrDeleted(invoker)
                || !OnRuneTile(rune, invoker) || !_mobState.IsAlive(invoker) || HasComp<SleepingComponent>(invoker)
                || _mobState.IsCritical(uid))
            {
                dissolve.Add(uid);
                continue;
            }
            _cult.Damage(invoker, "Blunt", 0.1f);
        }
        foreach (var uid in dissolve)
            DissolveCultGhost((uid, Comp<CultSpiritGhostComponent>(uid)));

        UpdateDarkSpirits(now);
    }
}

/// <summary>Вызывается, когда жертва принесена на руне подношения (совместимость со старыми целями).</summary>
public sealed class CultSacrificeCompletedEvent : EntityEventArgs
{
    public EntityUid Victim { get; }
    public EntityUid? VictimMind { get; }

    public CultSacrificeCompletedEvent(EntityUid victim, EntityUid? victimMind = null)
    {
        Victim = victim;
        VictimMind = victimMind;
    }
}

/// <summary>Вызывается, когда призвана Нар'Си.</summary>
public sealed class CultNarSieSummonedEvent : EntityEventArgs;
