using System.Numerics;
using Content.Server.Damage.Systems;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Popups;
using Content.Server.Imperial.Antimagic;
using Content.Shared.Bed.Sleep;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Electrocution;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Rust;
using Content.Shared.Medical;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Slippery;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Imperial.Heretic.Prototypes;

namespace Content.Server.Imperial.Heretic.Paths.Rust;

/// <summary>
/// Способности пути Ржавчины.
/// </summary>
public sealed class HereticRustActionsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ThrowingSystem _throw = default!;
    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly ImperialAntimagicSystem _antimagic = default!;

    private static readonly DamageSpecifier CorrodeBluntDamage = new() { DamageDict = { ["Blunt"] = 10 } };
    private static readonly DamageSpecifier InorganicDamage = new() { DamageDict = { ["Blunt"] = 500 } };
    private static readonly DamageSpecifier RustWaveCausticDamage = new() { DamageDict = { ["Caustic"] = 30 } };
    private static readonly DamageSpecifier RustConstructionBluntDamage = new() { DamageDict = { ["Blunt"] = 10 } };

    private static readonly EntProtoId HereticEffectSmoke = "HereticEffectSmoke";
    private static readonly ProtoId<HereticKnowledgePrototype> KnowledgeToxicBladeHereticKnowledge = "KnowledgeToxicBlade";
    private static readonly EntProtoId HereticRustWall = "HereticRustWall";
    private static readonly ProtoId<HereticKnowledgePrototype> KnowledgeMarkOfRustHereticKnowledge = "KnowledgeMarkOfRust";
    private static readonly EntProtoId HereticEffectEntropicPlume = "HereticEffectEntropicPlume";
    private static readonly EntProtoId HereticRustOverlay = "HereticRustOverlay";
    private static readonly EntProtoId HereticEffectCloudSwirl = "HereticEffectCloudSwirl";

    private static readonly TimeSpan CorrodeTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EntropicPlagueBlindDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EntropicPlagueDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TickDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RustConstructionTime = TimeSpan.FromSeconds(5);

    private const float RustWaveRange = 8f;
    private const float EntropicPlagueRange = 4f;
    private const float RustingCrownRange = 8f;
    private const float AggressiveSpreadRadius = 2f;
    private const float HereticRustWalkerAggressiveSpreadRange = 0.4f;
    private const float RustConstructionRange = 0.6f;
    private const float RustConstructionBaseThrowSpeed = 3f;
    private const float RustConstructionRange2 = 0.4f;
    private const float EntropicPlumeRange = 0.4f;
    private const float EntropicPlumeRange2 = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticRustBladeComponent, MeleeHitEvent>(OnRustBladeMeleeHit);
        SubscribeLocalEvent<HereticComponent, HereticCorrodeActionEvent>(OnCorrode);
        SubscribeLocalEvent<HereticComponent, HereticRustCoatActionEvent>(OnRustCoat);
        SubscribeLocalEvent<HereticComponent, HereticRustWaveActionEvent>(OnRustWave);
        SubscribeLocalEvent<HereticComponent, HereticEntropicPlagueActionEvent>(OnEntropicPlague);
        SubscribeLocalEvent<HereticComponent, HereticRustingCrownActionEvent>(OnRustingCrown);
        SubscribeLocalEvent<HereticComponent, HereticAggressiveSpreadActionEvent>(OnAggressiveSpread);
        SubscribeLocalEvent<HereticComponent, HereticRustWalkerAggressiveSpreadActionEvent>(OnHereticRustWalkerAggressiveSpread);
        SubscribeLocalEvent<HereticComponent, HereticRustConstructionActionEvent>(OnRustConstruction);
        SubscribeLocalEvent<HereticComponent, HereticEntropicPlumeActionEvent>(OnEntropicPlume);
        SubscribeLocalEvent<HereticComponent, HereticAscensionRustActionEvent>(OnAscensionRust);
        SubscribeLocalEvent<HereticRustAscendedComponent, DamageModifyEvent>(OnRustAscendedDamageModify);
        SubscribeLocalEvent<HereticRustAscendedComponent, StunnedEvent>(OnRustAscendedStunned);
        SubscribeLocalEvent<HereticRustAscendedComponent, KnockDownAttemptEvent>(OnRustAscendedKnockdownAttempt);
        SubscribeLocalEvent<HereticRustAscendedComponent, SlipAttemptEvent>(OnRustAscendedSlipAttempt);
        SubscribeLocalEvent<HereticRustAscendedComponent, ElectrocutionAttemptEvent>(OnRustAscendedElectrocution);
        SubscribeLocalEvent<HereticRustAscendedComponent, TryingToSleepEvent>(OnRustAscendedSleepAttempt);
        SubscribeLocalEvent<HereticPathAscendedEvent>(OnPathAscended);
    }

    // ─── Rust ─────────────────────────────────────────────────────────────────

    private void OnCorrode(EntityUid uid, HereticComponent comp, HereticCorrodeActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        if (args.Entity is not { } target)
            return;

        // Wiki: huge damage against inorganic/robotic targets
        if (TryComp<DamageableComponent>(target, out var damageable) &&
            damageable.DamageContainerID is { } containerId &&
            (containerId.Id == "Inorganic" || containerId.Id == "Silicon"))
        {
            var inorganicDmg = new DamageSpecifier(InorganicDamage);
            _damage.TryChangeDamage(target, inorganicDmg, ignoreResistances: false);
            Spawn(HereticEffectSmoke, Transform(target).Coordinates);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
            _popup.PopupEntity(Loc.GetString("heretic-corrode"), target, target, PopupType.LargeCaution);
            return;
        }

        // Toxic Blade: lets Corrode rust through Titanium/Plastitanium-grade structures
        if (_heretic.HasKnowledge(comp, KnowledgeToxicBladeHereticKnowledge) &&
            TryComp<DamageableComponent>(target, out var structDamageable) &&
            structDamageable.DamageContainerID is { Id: "StructuralInorganic" })
        {
            var coords = Transform(target).Coordinates;
            QueueDel(target);
            Spawn(HereticRustWall, coords);
            Spawn(HereticEffectSmoke, coords);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
            _popup.PopupEntity(Loc.GetString("heretic-corrode-structure"), uid, uid, PopupType.LargeCaution);
            return;
        }

        // SS13 Corrode (Mansus Grasp): 10 BRUTE + 80 stamina loss + Knockdown 5s
        var dmg = new DamageSpecifier(CorrodeBluntDamage);
        _damage.TryChangeDamage(target, dmg, ignoreResistances: false);
        _stamina.TakeStaminaDamage(target, 80f, visual: false);
        _stun.TryKnockdown(target, CorrodeTime, true);
        Spawn(HereticEffectSmoke, Transform(target).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-corrode"), target, target, PopupType.MediumCaution);
    }

    private void OnRustCoat(EntityUid uid, HereticComponent comp, HereticRustCoatActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        _heretic.RustTile(args.Target);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-rust-coat"), uid, uid, PopupType.Medium);
    }

    private void OnRustBladeMeleeHit(Entity<HereticRustBladeComponent> blade, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;
        if (!TryComp<HereticComponent>(args.User, out var comp))
            return;
        if (comp.CurrentPath != HereticPath.Rust)
            return;

        var hasMarkOfRust = _heretic.HasKnowledge(comp, KnowledgeMarkOfRustHereticKnowledge);
        var hasToxicBlade = _heretic.HasKnowledge(comp, KnowledgeToxicBladeHereticKnowledge);
        if (!hasMarkOfRust && !hasToxicBlade)
            return;

        foreach (var target in args.HitEntities)
        {
            if (hasMarkOfRust && HasComp<RustMarkComponent>(target))
            {
                RemCompDeferred<RustMarkComponent>(target);
                _stun.TryKnockdown(target, blade.Comp.RustBladeMeleeHitTime, true);
                _popup.PopupEntity(Loc.GetString("heretic-rust-mark-triggered"), args.User, args.User, PopupType.Medium);
            }

            if (hasToxicBlade)
                _vomit.Vomit(target, force: true);
        }
    }

    private void OnRustWave(EntityUid uid, HereticComponent comp, HereticRustWaveActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        // SS13 Patron's Reach (rust_wave): 30 TOX damage, no knockdown
        var dmg = new DamageSpecifier(RustWaveCausticDamage);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, RustWaveRange))
        {
            if (ent.Owner == uid)
                continue;
            if (_antimagic.CanBlockMagic(ent.Owner))
                continue;
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
        }
        Spawn(HereticEffectEntropicPlume, Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-rust-wave"), uid, uid, PopupType.Large);
    }

    private void OnEntropicPlague(EntityUid uid, HereticComponent comp, HereticEntropicPlagueActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        if (args.Entity is not { } target)
            return;
        // SS13: amok + cloudstruck/disorient; no direct HP damage.
        var blindDuration = EntropicPlagueBlindDuration;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(target).Coordinates, EntropicPlagueRange))
        {
            if (ent.Owner == uid)
                continue;
            if (_antimagic.CanBlockMagic(ent.Owner))
                continue;
            _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(ent.Owner, TemporaryBlindnessSystem.BlindingStatusEffect, blindDuration, true);
            _hereticEffects.ApplyInsanity(ent.Owner, EntropicPlagueDuration);
        }
        Spawn(HereticEffectEntropicPlume, Transform(target).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-entropic-plague"), uid, uid, PopupType.LargeCaution);
    }

    private void OnRustingCrown(EntityUid uid, HereticComponent comp, HereticRustingCrownActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var now = _timing.CurTime;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, RustingCrownRange))
        {
            if (ent.Owner == uid)
                continue;
            if (_antimagic.CanBlockMagic(ent.Owner))
                continue;
            var mark = EnsureComp<HereticRustingCrownMarkComponent>(ent.Owner);
            mark.TicksRemaining = 6;
            mark.NextTick = now + TickDelay;
            _popup.PopupEntity(Loc.GetString("heretic-rusting-crown-marked"), ent.Owner, ent.Owner, PopupType.SmallCaution);
        }

        Spawn(HereticEffectSmoke, Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-rusting-crown"), uid, uid, PopupType.Large);
    }

    // ─── Rust (expanded) ─────────────────────────────────────────────────────

    private void OnAggressiveSpread(EntityUid uid, HereticComponent comp, HereticAggressiveSpreadActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        _heretic.SpreadRustNearby(Transform(uid).Coordinates, AggressiveSpreadRadius);
        Spawn(HereticEffectEntropicPlume, Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-aggressive-spread"), uid, uid, PopupType.Large);
    }

    private static readonly string[] RustRuneEffects = {
        "HereticSmallRuneEffect1", "HereticSmallRuneEffect4", "HereticSmallRuneEffect7", "HereticSmallRuneEffect10"
    };

    private void OnHereticRustWalkerAggressiveSpread(EntityUid uid, HereticComponent comp, HereticRustWalkerAggressiveSpreadActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var mapCoords = _xform.GetMapCoordinates(uid);
        for (var dx = -2; dx <= 2; dx++)
            for (var dy = -2; dy <= 2; dy++)
            {
                if (dx * dx + dy * dy > 4)
                    continue;
                var pos = new MapCoordinates(mapCoords.Position + new Vector2(dx, dy), mapCoords.MapId);
                if (_lookup.GetEntitiesInRange<HereticRustOverlayComponent>(pos, HereticRustWalkerAggressiveSpreadRange).Count == 0)
                    Spawn(HereticRustOverlay, pos);
                Spawn(_random.Pick(RustRuneEffects), pos);
            }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_items_tools_welder.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-rust-walker-aggressive-spread"), uid, uid, PopupType.Large);
    }

    private void OnRustConstruction(EntityUid uid, HereticComponent comp, HereticRustConstructionActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (!_heretic.IsTileRusted(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("heretic-rust-construction-fail"), uid, uid, PopupType.MediumCaution);
            return;
        }

        var coords = args.Target.SnapToGrid(EntityManager);
        var mapCoords = _xform.ToMapCoordinates(coords);
        var tilePos = mapCoords.Position;

        var dmg = new DamageSpecifier(RustConstructionBluntDamage);

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, RustConstructionRange))
        {
            if (_mobs.IsDead(ent.Owner))
                continue;
            if (HasComp<HereticComponent>(ent.Owner))
                continue;

            var mobPos = _xform.GetMapCoordinates(ent.Owner).Position;
            var dir = mobPos - tilePos;
            if (dir.Length() > 0.01f)
                _throw.TryThrow(ent.Owner, dir.Normalized() * 3f, RustConstructionBaseThrowSpeed);

            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
            _stun.TryKnockdown(ent.Owner, RustConstructionTime, true);
        }

        Spawn(HereticRustWall, coords);

        if (_lookup.GetEntitiesInRange<HereticRustOverlayComponent>(mapCoords, RustConstructionRange2).Count == 0)
            Spawn(HereticRustOverlay, mapCoords);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/constructform.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-rust-construction"), uid, uid, PopupType.Large);
    }

    private void OnEntropicPlume(EntityUid uid, HereticComponent comp, HereticEntropicPlumeActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var casterCoords = _xform.ToMapCoordinates(Transform(uid).Coordinates);
        var targetCoords = _xform.ToMapCoordinates(args.Target);

        var dir = targetCoords.Position - casterCoords.Position;
        if (dir.LengthSquared() < 0.01f)
            dir = new Vector2(1f, 0f);
        dir = dir.Normalized();

        var right = new Vector2(dir.Y, -dir.X);

        var plumeEnt = Spawn(HereticEffectEntropicPlume, Transform(uid).Coordinates);
        _xform.SetWorldRotation(plumeEnt, Angle.FromWorldVec(dir));

        int[] halfWidths = { 0, 1, 2 };
        var blocked = new bool[5]; // columns -2..2 mapped to indices 0..4

        for (var row = 0; row < 3; row++)
        {
            var hw = halfWidths[row];
            for (var col = -hw; col <= hw; col++)
            {
                var idx = col + 2;
                if (blocked[idx])
                    continue;

                var tilePos = casterCoords.Position + dir * (row + 1) + right * col;
                var tileCoords = new MapCoordinates(tilePos, casterCoords.MapId);

                var isWall = false;
                foreach (var ent in _lookup.GetEntitiesInRange<PhysicsComponent>(tileCoords, EntropicPlumeRange, LookupFlags.Static))
                {
                    if ((ent.Comp.CollisionLayer & (int)CollisionGroup.Impassable) != 0)
                    {
                        isWall = true;
                        break;
                    }
                }

                if (isWall)
                {
                    blocked[idx] = true;
                    continue;
                }

                if (_lookup.GetEntitiesInRange<HereticRustOverlayComponent>(tileCoords, EntropicPlumeRange).Count == 0)
                    Spawn(HereticRustOverlay, tileCoords);

                foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(tileCoords, EntropicPlumeRange2))
                {
                    if (mob.Owner == uid || HasComp<HereticComponent>(mob.Owner))
                        continue;
                    if (_antimagic.CanBlockMagic(mob.Owner))
                        continue;

                    EnsureComp<HereticAmokComponent>(mob.Owner);
                    Spawn(HereticEffectCloudSwirl, Transform(mob.Owner).Coordinates);
                }
            }
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-entropic-plume"), uid, uid, PopupType.Large);
    }

    private void OnAscensionRust(EntityUid uid, HereticComponent comp, HereticAscensionRustActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }

        // Wiki: transmute 3 corpses nearby to Ascend.
        const float corpseSearchRadius = 5f;
        var coords = Transform(uid).Coordinates;
        var corpses = new List<EntityUid>();
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, corpseSearchRadius))
        {
            if (ent.Owner == uid)
                continue;
            if (_mobs.IsDead(ent.Owner, ent.Comp))
                corpses.Add(ent.Owner);
            if (corpses.Count >= 3)
                break;
        }

        if (corpses.Count < 3)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ascension-rust-fail"), uid, uid, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        foreach (var corpse in corpses)
            Del(corpse);

        // Wiki: while on rust tiles, healing is tripled and resistant to a wide variety of effects.
        EnsureComp<HereticRustAscendedComponent>(uid);

        // SS13: on ascension, rust spreads globally across the station.
        _heretic.TriggerRustAscensionWave(coords);

        Spawn(HereticEffectSmoke, coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_rust.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-rust"), uid, uid, PopupType.Large);
    }

    private void OnRustAscendedDamageModify(EntityUid uid, HereticRustAscendedComponent comp, DamageModifyEvent args)
    {
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates))
            return;

        var total = args.Damage.GetTotal();
        if (total < FixedPoint2.Zero)
        {
            // Healing — triple it.
            args.Damage *= 3;
        }
        else if (total > FixedPoint2.Zero)
        {
            // Resistance to a wide variety of effects — soften incoming damage.
            args.Damage *= 0.5f;
        }
    }

    private void OnRustAscendedStunned(EntityUid uid, HereticRustAscendedComponent comp, ref StunnedEvent args)
    {
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates))
            return;

        _stun.TryUnstun(uid);
        _statusEffects.TryRemoveStatusEffect(uid, "SlowedDown");
    }

    private void OnRustAscendedKnockdownAttempt(EntityUid uid, HereticRustAscendedComponent comp, ref KnockDownAttemptEvent args)
    {
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates))
            return;

        args.Cancelled = true;
    }

    private void OnRustAscendedSlipAttempt(EntityUid uid, HereticRustAscendedComponent comp, SlipAttemptEvent args)
    {
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates))
            return;

        args.NoSlip = true;
    }

    private void OnRustAscendedElectrocution(EntityUid uid, HereticRustAscendedComponent comp, ElectrocutionAttemptEvent args)
    {
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates))
            return;

        args.SiemensCoefficient = 0f;
    }

    private void OnRustAscendedSleepAttempt(EntityUid uid, HereticRustAscendedComponent comp, ref TryingToSleepEvent args)
    {
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates))
            return;

        args.Cancelled = true;
    }

    private void OnPathAscended(ref HereticPathAscendedEvent args)
    {
        if (args.Component.CurrentPath != HereticPath.Rust)
            return;

        var uid = args.Heretic;
        var coords = Transform(uid).Coordinates;

        // При авто-триггере: выдаём RustAscended (трупы уже потреблены ритуалом)
        EnsureComp<HereticRustAscendedComponent>(uid);
        _heretic.TriggerRustAscensionWave(coords);
        Spawn(HereticEffectSmoke, coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_rust.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-rust"), uid, uid, PopupType.Large);
    }
}
