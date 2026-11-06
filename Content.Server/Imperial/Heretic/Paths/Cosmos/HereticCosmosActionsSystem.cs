using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Cosmos;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Damage.Prototypes;

namespace Content.Server.Imperial.Heretic.Paths.Cosmos;

/// <summary>
/// Способности пути Космоса.
/// </summary>
public sealed class HereticCosmosActionsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;

    private static readonly EntProtoId HereticCosmicCarpet = "HereticCosmicCarpet";
    private static readonly EntProtoId HereticEffectCosmicCloud = "HereticEffectCosmicCloud";
    private static readonly ProtoId<DamageTypePrototype> RadiationDamageType = "Radiation";
    private static readonly EntProtoId HereticEffectSpaceExplosion = "HereticEffectSpaceExplosion";
    private static readonly EntProtoId MobHereticStarGazer = "MobHereticStarGazer";

    private static readonly TimeSpan AscensionCosmosDuration = TimeSpan.FromSeconds(30);

    private const float AscensionCosmosRange = 7f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticCosmosBladeComponent, MeleeHitEvent>(OnCosmosBladeMeleeHit);
        SubscribeLocalEvent<HereticComponent, HereticAscensionCosmosActionEvent>(OnAscensionCosmos);
        SubscribeLocalEvent<HereticPathAscendedEvent>(OnPathAscended);
        SubscribeLocalEvent<HereticRemovedEvent>(OnHereticRemoved);
    }

    private void OnAscensionCosmos(EntityUid uid, HereticComponent comp, HereticAscensionCosmosActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }

        const float corpseSearchRadius = 7f;
        var coords = Transform(uid).Coordinates;
        var markedCorpses = new List<EntityUid>();
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, corpseSearchRadius))
        {
            if (ent.Owner == uid)
                continue;
            if (_mobs.IsDead(ent.Owner, ent.Comp) && HasComp<StarMarkComponent>(ent.Owner))
                markedCorpses.Add(ent.Owner);
            if (markedCorpses.Count >= 3)
                break;
        }

        if (markedCorpses.Count < 3)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ascension-cosmos-fail"), uid, uid, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        var xform = Transform(uid);
        var parentUid = xform.ParentUid;
        var center = new Vector2(MathF.Floor(xform.LocalPosition.X) + 0.5f, MathF.Floor(xform.LocalPosition.Y) + 0.5f);
        int passiveLevel = comp.PassiveLevel;

        var cardinals = new[]
        {
            new Vector2(0, 1), new Vector2(0, -1),
            new Vector2(1, 0), new Vector2(-1, 0)
        };
        foreach (var dir in cardinals)
        {
            for (var i = 1; i <= 3; i++)
            {
                var pos = center + dir * i;
                var carpet = Spawn(HereticCosmicCarpet, new EntityCoordinates(parentUid, pos));
                if (passiveLevel > 0 && TryComp<HereticCosmicFieldComponent>(carpet, out var field))
                    field.PassiveLevel = passiveLevel;
            }
        }

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, AscensionCosmosRange))
        {
            if (ent.Owner == uid)
                continue;
            if (HasComp<HereticComponent>(ent.Owner))
                continue;
            _hereticEffects.AddStarMark(ent.Owner, AscensionCosmosDuration);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_magic_cosmic_expansion.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-cosmos-action"), uid, uid, PopupType.Large);
    }

    private void OnHereticRemoved(ref HereticRemovedEvent args)
    {
        RemCompDeferred<HereticCosmosComboComponent>(args.Heretic);
    }

    // ─── Cosmos blade (combo) ─────────────────────────────────────────────────

    private void OnCosmosBladeMeleeHit(Entity<HereticCosmosBladeComponent> blade, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;
        if (!TryComp<HereticComponent>(args.User, out var herComp))
            return;
        if (herComp.CurrentPath != HereticPath.Cosmos)
            return;

        var now = _timing.CurTime;

        var combo = EnsureComp<HereticCosmosComboComponent>(args.User);

        if (combo.ComboCount > 0 && now > combo.ResetAt)
        {
            combo.FirstTarget = null;
            combo.SecondTarget = null;
            combo.ComboCount = 0;
        }

        foreach (var target in args.HitEntities)
        {
            if (!HasComp<MobStateComponent>(target))
                continue;
            if (target == args.User)
                continue;

            if (HasComp<CosmosMarkComponent>(target))
            {
                if (TryComp<CosmosMarkComponent>(target, out var cosmosMark)
                    && cosmosMark.AnchorEntity.HasValue && !TerminatingOrDeleted(cosmosMark.AnchorEntity.Value))
                    QueueDel(cosmosMark.AnchorEntity.Value);
                RemCompDeferred<CosmosMarkComponent>(target);
                var currentPos = _xform.GetWorldPosition(target);
                var angle = _random.NextFloat(0f, MathF.PI * 2f);
                var dist = _random.NextFloat(3f, 6f);
                var teleportPos = currentPos + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_repulse.ogg"), target);
                Spawn(HereticEffectCosmicCloud, Transform(target).Coordinates);
                _xform.SetWorldPosition(target, teleportPos);
                Spawn(HereticEffectCosmicCloud, Transform(target).Coordinates);
                _stun.TryKnockdown(target, blade.Comp.CosmosBladeMeleeHitTime, true);
            }

            _hereticEffects.AddStarMark(target, blade.Comp.CosmosBladeMeleeHitDuration);

            _damage.TryChangeDamage(target,
                new DamageSpecifier { DamageDict = { [RadiationDamageType] = FixedPoint2.New(5) } },
                ignoreResistances: false);

            if (combo.ComboCount == 0)
            {
                combo.FirstTarget = target;
                combo.ComboCount = 1;
                combo.ResetAt = now + combo.ComboWindow;
            }
            else if (combo.ComboCount == 1 && target != combo.FirstTarget)
            {
                combo.SecondTarget = target;
                combo.ComboCount = 2;
                combo.ResetAt = now + combo.ComboWindow;

                _damage.TryChangeDamage(target,
                    new DamageSpecifier { DamageDict = { [RadiationDamageType] = FixedPoint2.New(14) } },
                    ignoreResistances: false);

                Spawn(HereticEffectSpaceExplosion, Transform(target).Coordinates);
            }
            else if (combo.ComboCount == 2 && target != combo.FirstTarget && target != combo.SecondTarget)
            {
                combo.ComboCount = 3;

                _damage.TryChangeDamage(target,
                    new DamageSpecifier { DamageDict = { [RadiationDamageType] = FixedPoint2.New(28) } },
                    ignoreResistances: false);

                Spawn(HereticEffectSpaceExplosion, Transform(target).Coordinates);
                combo.ResetAt = now + combo.ComboWindow;
            }

            break;
        }
    }

    private void OnPathAscended(ref HereticPathAscendedEvent args)
    {
        if (args.Component.CurrentPath != HereticPath.Cosmos)
            return;

        var uid = args.Heretic;
        var coords = Transform(uid).Coordinates;

        RemComp<RespiratorComponent>(uid);
        if (TryComp<TemperatureDamageComponent>(uid, out var tempDmg))
        {
            tempDmg.ColdDamageThreshold = 0f;
            tempDmg.HeatDamageThreshold = 99999f;
        }
        EnsureComp<PressureImmunityComponent>(uid);
        if (TryComp<EyeComponent>(uid, out var eye))
            _eye.SetDrawFov(uid, false, eye);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_cosmic.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-cosmos"), uid, uid, PopupType.Large);
        var gazerUid = Spawn(MobHereticStarGazer, coords);
        var gazerComp = EnsureComp<HereticStarGazerComponent>(gazerUid);
        gazerComp.Master = uid;
    }
}
