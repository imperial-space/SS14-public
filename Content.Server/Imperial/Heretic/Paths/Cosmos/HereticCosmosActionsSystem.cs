using System.Linq;
using System.Numerics;
using System.Threading;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Beam;
using Content.Server.Body;
using Content.Server.Body.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Damage.Systems;
using Content.Server.Decals;
using Content.Server.DoAfter;
using Content.Server.Doors.Systems;
using Content.Server.Imperial.Heretic.Components;
using Content.Server.Imperial.Heretic.Paths.Cosmos.Components;
using Content.Server.Mind;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.CombatMode;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Electrocution;
using Content.Shared.Eye;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Imperial.Lavaland.ColossusLoot;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Medical;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.SSDIndicator;
using Content.Shared.Slippery;
using Content.Shared.Speech.Muting;
using Content.Shared.Stacks;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.Server.Imperial.Heretic.Paths.Cosmos;

/// <summary>
/// Способности пути Космоса.
/// </summary>
public sealed class HereticCosmosActionsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem      _damage  = default!;
    [Dependency] private readonly EntityLookupSystem    _lookup  = default!;
    [Dependency] private readonly MobStateSystem        _mobs    = default!;
    [Dependency] private readonly PopupSystem           _popup   = default!;
    [Dependency] private readonly IRobustRandom         _random  = default!;
    [Dependency] private readonly SharedStunSystem      _stun    = default!;
    [Dependency] private readonly SharedTransformSystem _xform   = default!;
    [Dependency] private readonly HereticSystem         _heretic = default!;
    [Dependency] private readonly SharedAudioSystem     _audio   = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly IGameTiming            _timing       = default!;
    [Dependency] private readonly SharedEyeSystem        _eye          = default!;

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
        if (args.Handled) return;
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
            if (ent.Owner == uid) continue;
            if (_mobs.IsDead(ent.Owner, ent.Comp) && HasComp<StarMarkComponent>(ent.Owner))
                markedCorpses.Add(ent.Owner);
            if (markedCorpses.Count >= 3) break;
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
                var carpet = Spawn("HereticCosmicCarpet", new EntityCoordinates(parentUid, pos));
                if (passiveLevel > 0 && TryComp<HereticCosmicFieldComponent>(carpet, out var field))
                    field.PassiveLevel = passiveLevel;
            }
        }

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 7f))
        {
            if (ent.Owner == uid) continue;
            if (HasComp<HereticComponent>(ent.Owner)) continue;
            _hereticEffects.AddStarMark(ent.Owner, TimeSpan.FromSeconds(30));
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
        if (!args.IsHit) return;
        if (!TryComp<HereticComponent>(args.User, out var herComp)) return;
        if (herComp.CurrentPath != HereticPath.Cosmos) return;

        var now = _timing.CurTime;

        var combo = EnsureComp<HereticCosmosComboComponent>(args.User);

        if (combo.ComboCount > 0 && now > combo.ResetAt)
        {
            combo.FirstTarget  = EntityUid.Invalid;
            combo.SecondTarget = EntityUid.Invalid;
            combo.ComboCount   = 0;
        }

        foreach (var target in args.HitEntities)
        {
            if (!HasComp<MobStateComponent>(target)) continue;
            if (target == args.User) continue;

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
                Spawn("HereticEffectCosmicCloud", Transform(target).Coordinates);
                _xform.SetWorldPosition(target, teleportPos);
                Spawn("HereticEffectCosmicCloud", Transform(target).Coordinates);
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(2), true);
            }

            _hereticEffects.AddStarMark(target, TimeSpan.FromSeconds(30));

            _damage.TryChangeDamage(target,
                new DamageSpecifier { DamageDict = { ["Radiation"] = FixedPoint2.New(5) } },
                ignoreResistances: false);

            if (combo.ComboCount == 0)
            {
                combo.FirstTarget = target;
                combo.ComboCount  = 1;
                combo.ResetAt     = now + combo.ComboWindow;
            }
            else if (combo.ComboCount == 1 && target != combo.FirstTarget)
            {
                combo.SecondTarget = target;
                combo.ComboCount   = 2;
                combo.ResetAt      = now + combo.ComboWindow;

                _damage.TryChangeDamage(target,
                    new DamageSpecifier { DamageDict = { ["Radiation"] = FixedPoint2.New(14) } },
                    ignoreResistances: false);

                Spawn("HereticEffectSpaceExplosion", Transform(target).Coordinates);
            }
            else if (combo.ComboCount == 2 && target != combo.FirstTarget && target != combo.SecondTarget)
            {
                combo.ComboCount = 3;

                _damage.TryChangeDamage(target,
                    new DamageSpecifier { DamageDict = { ["Radiation"] = FixedPoint2.New(28) } },
                    ignoreResistances: false);

                Spawn("HereticEffectSpaceExplosion", Transform(target).Coordinates);
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
        var gazerUid = Spawn("MobHereticStarGazer", coords);
        var gazerComp = EnsureComp<HereticStarGazerComponent>(gazerUid);
        gazerComp.Master = uid;
    }
}
