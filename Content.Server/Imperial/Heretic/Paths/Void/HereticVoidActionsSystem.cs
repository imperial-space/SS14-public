using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Popups;
using Content.Server.Imperial.Antimagic;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.CombatMode;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Rust;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Imperial.Heretic.Prototypes;

namespace Content.Server.Imperial.Heretic.Paths.Void;

/// <summary>
/// Способности пути Пустоты.
/// </summary>
public sealed class HereticVoidActionsSystem : EntitySystem
{
    [Dependency] private readonly Content.Server.Imperial.Helpers.Schedule.ImperialScheduleSystem _schedule = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ThrowingSystem _throw = default!;
    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly HereticMoonBrainDamageSystem _brainDamage = default!;
    [Dependency] private readonly HereticVoidPrisonSystem _voidPrison = default!;
    [Dependency] private readonly ImperialAntimagicSystem _antimagic = default!;

    private static readonly DamageSpecifier VoidPullBluntDamage = new() { DamageDict = { ["Blunt"] = 30 } };
    private static readonly DamageSpecifier VoidPhaseBluntDamage = new() { DamageDict = { ["Blunt"] = 40 } };
    private static readonly DamageSpecifier WaveOfDesperationBluntDamage = new() { DamageDict = { ["Blunt"] = 15 } };

    private static readonly EntProtoId HereticEffectVoidBlinkIn = "HereticEffectVoidBlinkIn";
    private static readonly EntProtoId HereticVoidConduit = "HereticVoidConduit";
    private static readonly ProtoId<HereticKnowledgePrototype> KnowledgeVoidBladeUpgradeHereticKnowledge = "KnowledgeVoidBladeUpgrade";
    private static readonly EntProtoId HereticEffectVoidBlinkOut = "HereticEffectVoidBlinkOut";
    private static readonly EntProtoId HereticEffectRingleader = "HereticEffectRingleader";
    private static readonly EntProtoId HereticLastResortStatusEffect = "HereticLastResortStatusEffect";
    private static readonly EntProtoId EffectVoidBlink = "EffectVoidBlink";
    private static readonly EntProtoId MobHereticMaidInMirror = "MobHereticMaidInMirror";
    private static readonly EntProtoId HereticEffectSpaceExplosion = "HereticEffectSpaceExplosion";

    private static readonly TimeSpan VoidPullDuration = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan VoidPullTime = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan WaveOfDesperationTime = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan WaveOfDesperationDuration = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan WaveOfDesperationDelay = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan WaveOfDesperationTime2 = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ApplyWaveMansusTouchMoonDuration = TimeSpan.FromSeconds(10);

    private const float VoidPullRange = 2f;
    private const float VoidPhaseRange = 2.99f;
    private const float WaveOfDesperationRange = 3f;
    private const float WaveOfDesperationBaseThrowSpeed = 4f;
    private const float ApplyWaveMansusTouchMoonAmount = 20f;
    private const float ApplyWaveMansusTouchFleshAmount = 6f;
    private const float ApplyWaveMansusTouchBladeAmount = 6f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, HereticVoidPullActionEvent>(OnVoidPull);
        SubscribeLocalEvent<HereticComponent, HereticVoidConduitActionEvent>(OnVoidConduit);
        SubscribeLocalEvent<HereticVoidBladeComponent, MeleeHitEvent>(OnVoidBladeMeleeHit);
        SubscribeLocalEvent<HereticComponent, HereticVoidPhaseActionEvent>(OnVoidPhase);
        SubscribeLocalEvent<HereticComponent, HereticVoidPrisonActionEvent>(OnVoidPrison);
        SubscribeLocalEvent<HereticComponent, HereticWaveOfDesperationActionEvent>(OnWaveOfDesperation);
        SubscribeLocalEvent<HereticComponent, HereticMaidInMirrorActionEvent>(OnMaidInMirror);
        SubscribeLocalEvent<HereticComponent, HereticVoidSeekingBladeActionEvent>(OnVoidSeekingBlade);
        SubscribeLocalEvent<HereticPathAscendedEvent>(OnPathAscended);
    }

    // ─── Void ─────────────────────────────────────────────────────────────────

    private void OnVoidPull(EntityUid uid, HereticComponent comp, HereticVoidPullActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var myLocalPos = Transform(uid).LocalPosition;
        var coords = Transform(uid).Coordinates;

        var dmg = new DamageSpecifier(VoidPullBluntDamage);

        // aoe_radius = 2
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, VoidPullRange))
        {
            if (ent.Owner == uid)
                continue;
            if (_antimagic.CanBlockMagic(ent.Owner))
                continue;
            if (HasComp<HereticComponent>(ent.Owner))
                continue;
            if (_mobs.IsDead(ent.Owner))
                continue;
            if (Transform(ent.Owner).ParentUid != Transform(uid).ParentUid)
                continue;

            var targetLocalPos = Transform(ent.Owner).LocalPosition;
            var dir = myLocalPos - targetLocalPos;
            var dist = dir.Length();

            _stun.TryAddParalyzeDuration(ent.Owner, VoidPullDuration);
            _stun.TryKnockdown(ent.Owner, VoidPullTime, true);

            if (dist > 0.01f)
            {
                var moveAmount = Math.Min(3f, dist);
                _xform.SetLocalPosition(ent.Owner, targetLocalPos + dir.Normalized() * moveAmount);
            }

            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
            _hereticEffects.ApplyVoidChill(ent.Owner, 3);
        }

        Spawn(HereticEffectVoidBlinkIn, coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/Eldritch/voidblink.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-void-pull-activate"), uid, uid, PopupType.Medium);
    }

    /// <summary>
    /// conjure/void_conduit: spell_requirements = NONE — фокус не нужен,
    /// summon_radius = 0 — врата открываются прямо на клетке еретика.
    /// </summary>
    private void OnVoidConduit(EntityUid uid, HereticComponent comp, HereticVoidConduitActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        _chat.TrySendInGameICMessage(uid, Loc.GetString("heretic-incantation-void-conduit"), InGameICChatType.Speak, false, ignoreActionBlocker: true);
        // conjure/void_conduit: sound = null — у самих врат свой гул.

        var conduit = Spawn(HereticVoidConduit, _xform.GetMapCoordinates(uid));
        if (TryComp<HereticVoidConduitComponent>(conduit, out var conduitComp))
            conduitComp.Caster = uid;

        _popup.PopupEntity(Loc.GetString("heretic-void-conduit-place"), uid, uid, PopupType.Large);
    }

    private void OnVoidBladeMeleeHit(Entity<HereticVoidBladeComponent> blade, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;
        if (!TryComp<HereticComponent>(args.User, out var comp))
            return;
        if (comp.CurrentPath != HereticPath.Void)
            return;

        // Холод клинок накладывает только после улучшения «Ищущий клинок».
        if (!_heretic.HasKnowledge(comp, KnowledgeVoidBladeUpgradeHereticKnowledge))
            return;

        foreach (var target in args.HitEntities)
        {
            if (target == args.User)
                continue;
            if (HasComp<HereticComponent>(target))
                continue;
            _hereticEffects.ApplyVoidChill(target, 2);
        }
    }

    private void OnVoidSeekingBlade(EntityUid uid, HereticComponent comp, HereticVoidSeekingBladeActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        var target = args.Target;

        if (!HasComp<VoidMarkComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("heretic-void-seeking-blade-no-mark"), uid, uid, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        RemComp<VoidMarkComponent>(target);

        var userPos = _xform.GetWorldPosition(Transform(uid));
        var targetPos = _xform.GetWorldPosition(Transform(target));

        var dir = targetPos - userPos;
        if (dir.Length() > 0.01f)
            dir = Vector2.Normalize(dir);

        var behindPos = targetPos - dir;
        _xform.SetWorldPosition(uid, behindPos);

        if (_melee.TryGetWeapon(uid, out var weaponUid, out var weapon))
        {
            var combatComp = EnsureComp<CombatModeComponent>(uid);
            var wasCombat = combatComp.IsInCombatMode;
            if (!wasCombat)
                _combatMode.SetInCombatMode(uid, true, combatComp);

            _melee.AttemptLightAttack(uid, weaponUid, weapon, target);

            if (!wasCombat)
                _combatMode.SetInCombatMode(uid, false, combatComp);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/Eldritch/voidblink.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-void-seeking-blade-activate"), uid, uid, PopupType.Medium);
    }

    // ─── Void (expanded) ─────────────────────────────────────────────────────

    private void OnVoidPhase(EntityUid uid, HereticComponent comp, HereticVoidPhaseActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        var srcCoords = Transform(uid).Coordinates;

        if (_xform.InRange(srcCoords, args.Target, VoidPhaseRange))
        {
            _popup.PopupEntity(Loc.GetString("heretic-void-phase-fail"), uid, uid, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        _chat.TrySendInGameICMessage(uid, Loc.GetString("heretic-incantation-void-phase"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);

        Spawn(HereticEffectVoidBlinkIn, srcCoords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/Eldritch/voidblink.ogg"), srcCoords);

        var aoe = new DamageSpecifier(VoidPhaseBluntDamage);

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(srcCoords, 1f))
        {
            if (ent.Owner == uid)
                continue;
            if (_antimagic.CanBlockMagic(ent.Owner))
                continue;
            if (HasComp<HereticComponent>(ent.Owner))
                continue;
            _damage.TryChangeDamage(ent.Owner, aoe, ignoreResistances: false);
            _hereticEffects.ApplyVoidChill(ent.Owner, 2);
        }

        _xform.SetWorldPosition(uid, _xform.ToMapCoordinates(args.Target).Position);
        var dstCoords = Transform(uid).Coordinates;
        Spawn(HereticEffectVoidBlinkOut, dstCoords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/Eldritch/voidblink.ogg"), dstCoords);

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(dstCoords, 1f))
        {
            if (ent.Owner == uid)
                continue;
            if (_antimagic.CanBlockMagic(ent.Owner))
                continue;
            if (HasComp<HereticComponent>(ent.Owner))
                continue;
            _damage.TryChangeDamage(ent.Owner, aoe, ignoreResistances: false);
            _hereticEffects.ApplyVoidChill(ent.Owner, 2);
        }

        _popup.PopupEntity(Loc.GetString("heretic-void-phase"), uid, uid, PopupType.Medium);
    }

    private void OnVoidPrison(EntityUid uid, HereticComponent comp, HereticVoidPrisonActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        // Заклинание наводится на одну цель: pointed/void_prison.
        var victim = args.Target;
        if (!HasComp<MobStateComponent>(victim) || victim == uid)
            return;

        if (HasComp<HereticComponent>(victim) || _mobs.IsDead(victim))
            return;

        if (_antimagic.CanBlockMagic(victim))
        {
            _popup.PopupEntity(Loc.GetString("heretic-void-prison-blocked"), victim, PopupType.MediumCaution);
            args.Handled = true;
            return;
        }

        args.Handled = true;

        var coords = Transform(uid).Coordinates;
        _chat.TrySendInGameICMessage(uid, Loc.GetString("heretic-incantation-void-prison"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/glass_break3.ogg"), coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/Eldritch/voidblink.ogg"), coords);

        _voidPrison.ApplyVoidPrison(victim);

        _popup.PopupEntity(Loc.GetString("heretic-void-prison"), uid, uid, PopupType.Medium);
    }

    private void OnWaveOfDesperation(EntityUid uid, HereticComponent comp, HereticWaveOfDesperationActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        if (!TryComp<CuffableComponent>(uid, out var cuffable) || cuffable.CuffedHandCount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-wave-of-desperation-fail"), uid, uid, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        // Instantly destroy all restraints — no DoAfter, this is an escape spell.
        foreach (var cuffEnt in cuffable.Container.ContainedEntities.ToArray())
            _cuffs.Uncuff(uid, uid, cuffEnt, cuffable);

        var coords = Transform(uid).Coordinates;
        var myPos = _xform.GetWorldPosition(uid);
        var dmg = new DamageSpecifier(WaveOfDesperationBluntDamage);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, WaveOfDesperationRange))
        {
            if (ent.Owner == uid)
                continue;
            if (_antimagic.CanBlockMagic(ent.Owner))
                continue;
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
            _stun.TryKnockdown(ent.Owner, WaveOfDesperationTime, true);
            ApplyWaveMansusTouch(uid, comp, ent.Owner);
            var dir = _xform.GetWorldPosition(ent.Owner) - myPos;
            if (dir.Length() > 0.01f)
                _throw.TryThrow(ent.Owner, dir.Normalized() * 3f, WaveOfDesperationBaseThrowSpeed);
        }
        Spawn(HereticEffectRingleader, coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/swap.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-wave-of-desperation"), uid, uid, PopupType.Large);

        // Speed boost for 12 seconds — caster ignores slowdowns until they fall unconscious.
        _movementMod.TryAddMovementSpeedModDuration(uid, HereticLastResortStatusEffect, WaveOfDesperationDuration, 1.3f);
        _popup.PopupEntity(Loc.GetString("heretic-wave-of-desperation-buff"), uid, uid, PopupType.Medium);

        // Caster loses consciousness 12 seconds after the spell fires (not immediately).
        var capturedUid = uid;
        _schedule.Schedule(WaveOfDesperationDelay, () =>
        {
            if (Deleted(capturedUid) || !_mobs.IsAlive(capturedUid))
                return;
            _stun.TryKnockdown(capturedUid, WaveOfDesperationTime2, true);
        });
    }

    private void ApplyWaveMansusTouch(EntityUid caster, HereticComponent comp, EntityUid target)
    {
        switch (comp.CurrentPath)
        {
            case HereticPath.Ash:
                _flammable.AdjustFireStacks(target, 2f, ignite: true);
                break;

            case HereticPath.Moon:
                _hereticEffects.ApplyHallucination(target, ApplyWaveMansusTouchMoonDuration);
                _brainDamage.AddBrainDamage(target, ApplyWaveMansusTouchMoonAmount);
                break;

            case HereticPath.Flesh when _heretic.HasKnowledge(comp, "KnowledgeMarkOfFlesh"):
                _bloodstream.TryModifyBleedAmount(target, ApplyWaveMansusTouchFleshAmount);
                break;

            case HereticPath.Void when _heretic.HasKnowledge(comp, "KnowledgeVoidTraveler"):
                _hereticEffects.ApplyVoidChill(target, 1);
                break;

            case HereticPath.Blade when _heretic.HasKnowledge(comp, "KnowledgeSanguineSurge"):
                _bloodstream.TryModifyBleedAmount(target, ApplyWaveMansusTouchBladeAmount);
                break;

            case HereticPath.Rust when _heretic.HasKnowledge(comp, "KnowledgeMarkOfRust"):
                EnsureComp<RustMarkComponent>(target);
                break;

        }
    }

    private void OnMaidInMirror(EntityUid uid, HereticComponent comp, HereticMaidInMirrorActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        var coords = Transform(uid).Coordinates;
        for (var i = 0; i < 3; i++)
        {
            var angle = i * (Math.PI * 2 / 3);
            var offset = new Vector2((float)Math.Cos(angle) * 1.5f, (float)Math.Sin(angle) * 1.5f);
            Spawn(EffectVoidBlink, coords.Offset(offset));
        }
        Spawn(MobHereticMaidInMirror, coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-maid-in-mirror"), uid, uid, PopupType.Large);
    }

    private void OnPathAscended(ref HereticPathAscendedEvent args)
    {
        if (args.Component.CurrentPath != HereticPath.Void)
            return;

        var uid = args.Heretic;
        var coords = Transform(uid).Coordinates;

        // SS13: TRAIT_RESISTLOWPRESSURE
        EnsureComp<PressureImmunityComponent>(uid);
        // SS13: TRAIT_NEGATES_GRAVITY + TRAIT_MOVE_FLYING
        var gravity = EnsureComp<MovementIgnoreGravityComponent>(uid);
        gravity.Weightless = true;
        Dirty(uid, gravity);
        // SS13: RegisterSignal(COMSIG_ATOM_PRE_BULLET_ACT) → 75% deflect back
        var reflect = EnsureComp<ReflectComponent>(uid);
        reflect.ReflectProb = 0.75f;
        Dirty(uid, reflect);
        // SS13: sound_loop = new(user, TRUE, TRUE) + COMSIG_LIVING_LIFE
        EnsureComp<HereticVoidAscendedComponent>(uid);
        Spawn(HereticEffectSpaceExplosion, coords);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-void"), uid, uid, PopupType.Large);
    }
}
