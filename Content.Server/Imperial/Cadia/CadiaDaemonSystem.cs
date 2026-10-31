using System.Numerics;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Content.Server.Imperial.ZLevels;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Демоны: сила зависит от стоящих пилонов, призывы у разломов за силу варпа, смерть возвращает в варп.
/// Малые разломы закрываются сами, принца демонов призывают только после падения половины пилонов или в развязке.
/// </summary>
public sealed class CadiaDaemonSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;
    [Dependency] private readonly ZLevelSystem _z = default!;

    private static readonly SoundSpecifier SummonSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/castsummon.ogg");
    private static readonly SoundSpecifier SummonDoneSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/exit_blood.ogg");
    private static readonly SoundSpecifier DeathSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/demon_dies.ogg");

    private static readonly Robust.Shared.Prototypes.EntProtoId FlyUpAction = "ActionCadiaFlyUp";
    private static readonly Robust.Shared.Prototypes.EntProtoId FlyDownAction = "ActionCadiaFlyDown";

    /// <summary>Пока стоят все пилоны: демон бьёт на 40% слабее и получает на 50% больше.</summary>
    private const float MaxWeakenOut = 0.4f;
    private const float MaxWeakenIn = 0.5f;
    private TimeSpan _nextRift;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaDaemonComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<CadiaDaemonComponent, GetMeleeDamageEvent>(OnMeleeDamage);
        SubscribeLocalEvent<CadiaDaemonComponent, MobStateChangedEvent>(OnState);
        SubscribeLocalEvent<CadiaDaemonComponent, MapInitEvent>(OnDaemonInit);
        SubscribeLocalEvent<CadiaWarpRiftComponent, MapInitEvent>(OnRiftInit);
        SubscribeLocalEvent<CadiaWarpRiftComponent, GetVerbsEvent<AlternativeVerb>>(OnRiftVerbs);
        SubscribeLocalEvent<CadiaWarpRiftComponent, CadiaSummonDoAfterEvent>(OnSummonDone);
        SubscribeLocalEvent<CadiaFlyUpActionEvent>(e => OnFly(e, true));
        SubscribeLocalEvent<CadiaFlyDownActionEvent>(e => OnFly(e, false));
    }

    private void OnDaemonInit(Entity<CadiaDaemonComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Boss)
            _rule.PrinceArrived();
        if (!ent.Comp.Flyer)
            return;
        // Летун не падает в проёмы и сам ходит между этажами.
        EnsureComp<ZFlyerComponent>(ent);
        _actions.AddAction(ent, FlyUpAction);
        _actions.AddAction(ent, FlyDownAction);
    }

    // ═══════════════════════ Сила демонов ═══════════════════════

    /// <summary>Доля стоящих пилонов: 1 — варп сдержан, 0 — варп свободен.</summary>
    private float PylonShare()
    {
        return _rule.TryGetActiveWar(out var war) && war.PylonsTotal > 0
            ? (float) war.PylonsAlive / war.PylonsTotal
            : 0f;
    }

    private void OnDamageModify(Entity<CadiaDaemonComponent> ent, ref DamageModifyEvent args)
    {
        var share = PylonShare();
        if (share > 0f)
            args.Damage *= 1f + MaxWeakenIn * share;
    }

    private void OnMeleeDamage(Entity<CadiaDaemonComponent> ent, ref GetMeleeDamageEvent args)
    {
        var share = PylonShare();
        if (share > 0f)
            args.Damage *= 1f - MaxWeakenOut * share;
    }

    private void OnState(Entity<CadiaDaemonComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var coords = _xform.GetMapCoordinates(ent);
        Spawn("CadiaPsyPerilsEffect", coords);
        _audio.PlayPvs(DeathSound, Spawn(null, coords));
        if (ent.Comp.SplitInto is { } split)
        {
            for (var i = 0; i < ent.Comp.SplitCount; i++)
                Spawn(split, coords.Offset(_random.NextVector2(0.8f)));
        }
        if (ent.Comp.Boss)
            _rule.BossBanished();
        QueueDel(ent);
    }

    // ═══════════════════════ Разломы ═══════════════════════

    private void OnRiftInit(Entity<CadiaWarpRiftComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Lifetime is { } life)
            ent.Comp.ClosesAt = _timing.CurTime + life;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextRift)
            return;
        _nextRift = now + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<CadiaWarpRiftComponent>();
        while (query.MoveNext(out var uid, out var rift))
        {
            if (rift.ClosesAt is { } closes && now >= closes)
                QueueDel(uid);
        }
    }

    /// <summary>Цена с учётом пилонов: пока все стоят — вдвое дороже.</summary>
    private int CostOf(CadiaSummonEntry entry)
    {
        return (int) Math.Ceiling(entry.Cost * (1f + PylonShare()));
    }

    private void OnRiftVerbs(Entity<CadiaWarpRiftComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<CadiaSummonerComponent>(args.User))
            return;
        var user = args.User;
        for (var i = 0; i < ent.Comp.Summons.Count; i++)
        {
            var entry = ent.Comp.Summons[i];
            var index = i;
            var open = !entry.RequiresFallenPylons || _rule.BossUnlocked();
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cadia-summon-verb", ("name", Loc.GetString(entry.Name)), ("cost", CostOf(entry))),
                Category = VerbCategory.SelectType,
                Disabled = !open,
                Message = open ? null : Loc.GetString("cadia-summon-locked"),
                Act = () => StartSummon(ent, user, index),
            });
        }
    }

    private void StartSummon(Entity<CadiaWarpRiftComponent> ent, EntityUid user, int index)
    {
        var entry = ent.Comp.Summons[index];
        if (!_rule.TryGetActiveWar(out var war) || war.WarpPower < CostOf(entry))
        {
            _popup.PopupEntity(Loc.GetString("cadia-summon-no-warp"), ent, user, PopupType.SmallCaution);
            return;
        }
        var doAfter = new DoAfterArgs(EntityManager, user, entry.Delay, new CadiaSummonDoAfterEvent(index), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };
        if (_doAfter.TryStartDoAfter(doAfter))
            _audio.PlayPvs(SummonSound, ent);
    }

    private void OnSummonDone(Entity<CadiaWarpRiftComponent> ent, ref CadiaSummonDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Index < 0 || args.Index >= ent.Comp.Summons.Count)
            return;
        args.Handled = true;
        var entry = ent.Comp.Summons[args.Index];
        if (entry.RequiresFallenPylons && !_rule.BossUnlocked())
            return;
        if (!_rule.TrySpendWarp(CostOf(entry)))
        {
            _popup.PopupEntity(Loc.GetString("cadia-summon-no-warp"), ent, args.User, PopupType.SmallCaution);
            return;
        }
        var coords = _xform.GetMapCoordinates(ent);
        Spawn(entry.Proto, coords.Offset(_random.NextVector2(1.2f)));
        Spawn("CadiaPsyRingRed", coords);
        _audio.PlayPvs(SummonDoneSound, ent);
    }

    // ═══════════════════════ Полёт между этажами ═══════════════════════

    private void OnFly(InstantActionEvent args, bool up)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var user = args.Performer;
        var here = _xform.GetMapCoordinates(user);
        if (_z.GetStep(here, up) is not { } target)
        {
            _popup.PopupEntity(Loc.GetString(up ? "zlevel-nothing-above" : "zlevel-nothing-below"), user, user);
            return;
        }
        _z.ZMove(user, target, false);
        _popup.PopupEntity(Loc.GetString(up ? "zlevel-fly-up" : "zlevel-fly-down"), user, user, PopupType.Small);
    }
}
