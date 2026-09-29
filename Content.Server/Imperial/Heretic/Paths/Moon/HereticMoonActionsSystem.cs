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
using Content.Server.Imperial.Heretic.Effects;
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
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
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

namespace Content.Server.Imperial.Heretic.Paths.Moon;

/// <summary>
/// Способности пути Луны.
/// </summary>
public sealed class HereticMoonActionsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem      _damage  = default!;
    [Dependency] private readonly EntityLookupSystem    _lookup  = default!;
    [Dependency] private readonly MobStateSystem        _mobs    = default!;
    [Dependency] private readonly PopupSystem           _popup   = default!;
    [Dependency] private readonly IRobustRandom         _random  = default!;
    [Dependency] private readonly SharedStunSystem      _stun    = default!;
    [Dependency] private readonly HereticSystem         _heretic = default!;
    [Dependency] private readonly SharedAudioSystem     _audio   = default!;
    [Dependency] private readonly InventorySystem        _inventory = default!;
    [Dependency] private readonly StatusEffectsSystem    _statusEffects = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly HereticMoonAmuletSystem      _moonAmulet      = default!;
    [Dependency] private readonly MetaDataSystem              _metaData      = default!;
    [Dependency] private readonly VisibilitySystem            _visibility    = default!;
    [Dependency] private readonly HereticMoonBrainDamageSystem _brainDamage   = default!;
    [Dependency] private readonly VisualBodySystem              _visualBody    = default!;
    [Dependency] private readonly IPrototypeManager             _prototype     = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, HereticMoonGateActionEvent>(OnMoonGate);
        SubscribeLocalEvent<HereticPathAscendedEvent>(OnPathAscended);
    }

    private void OnMoonGate(EntityUid uid, HereticComponent comp, HereticMoonGateActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        var target = args.Target;

        ApplyMoonGateEffects(uid, target);
        _popup.PopupEntity(Loc.GetString("heretic-moon-gate"), uid, uid, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("heretic-moon-gate-popup"), target, target, PopupType.LargeCaution);
    }

    private void ApplyMoonGateEffects(EntityUid uid, EntityUid target)
    {
        // SS13: living_owner.adjust_organ_loss(ORGAN_SLOT_BRAIN, 20, 140)
        _brainDamage.AddBrainDamage(uid, 20f, isCasterCapped: true);

        // SS13: cast_on.mob_mood.adjust_sanity(-20)
        if (!TryComp<HereticMoonBrainDamageComponent>(target, out var targetBrain))
            targetBrain = AddComp<HereticMoonBrainDamageComponent>(target);

        var targetSanity = targetBrain.Sanity;
        targetBrain.Sanity = Math.Max(0f, targetBrain.Sanity - 20f);

        // SS13: cast_on.adjust_oxy_loss(30)
        var oxyDmg = new DamageSpecifier();
        oxyDmg.DamageDict["Asphyxiation"] = FixedPoint2.New(30);
        _damage.TryChangeDamage(target, oxyDmg, ignoreResistances: false);

        // SS13: cast_on.cause_hallucination(body) + cause_hallucination(heretic/gate)
        _hereticEffects.ApplyHallucination(target, TimeSpan.FromSeconds(10));

        // SS13: cast_on.adjust_organ_loss(ORGAN_SLOT_BRAIN, 30)
        _brainDamage.AddBrainDamage(target, 30f, isCasterCapped: false);

        // SS13 duration formula: ((SANITY_MAXIMUM - sanity) / (SANITY_MAXIMUM - SANITY_INSANE)) * 15s + 1s
        // SANITY_MAXIMUM=150, SANITY_INSANE=40 → divisor = 110
        var durationSecs = Math.Clamp((150f - targetSanity) / 110f * 15f + 1f, 1f, 16f);
        var duration = TimeSpan.FromSeconds(durationSecs);

        // SS13: cast_on.adjust_temp_blindness(duration) + cast_on.adjust_silence(duration)
        _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(target, TemporaryBlindnessSystem.BlindingStatusEffect, duration, true);
        _statusEffects.TryAddStatusEffect<MutedComponent>(target, "Muted", duration, true);

        // SS13: cast_on.adjust_confusion(10 SECONDS) — insanity is the closest SS14 analog
        _hereticEffects.ApplyInsanity(target, TimeSpan.FromSeconds(10));

        // SS13: if(cast_on.mob_mood.sanity < 40) cast_on.AdjustKnockdown(2 SECONDS)
        if (targetSanity < 40f)
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(2), true);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);

        // Level 3: Gate усиливает амулет — canal_amulet на цель
        if (TryComp<HereticComponent>(uid, out var gateHeretic) && gateHeretic.PassiveLevel >= 3)
            _moonAmulet.ApplyChannelAmulet(uid, target, targetBrain);
    }

    private void OnPathAscended(ref HereticPathAscendedEvent args)
    {
        if (args.Component.CurrentPath != HereticPath.Moon)
            return;

        var uid = args.Heretic;
        var coords = Transform(uid).Coordinates;

        // Взрыв разума в радиусе 8f
        var headExplosionDmg = new DamageSpecifier();
        headExplosionDmg.DamageDict["Cellular"] = FixedPoint2.New(30);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 8f))
        {
            if (ent.Owner == uid) continue;
            _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(ent.Owner, TemporaryBlindnessSystem.BlindingStatusEffect, TimeSpan.FromSeconds(8), true);
            if (HasComp<MindShieldComponent>(ent.Owner))
                _damage.TryChangeDamage(ent.Owner, headExplosionDmg, ignoreResistances: false);
            else
                _hereticEffects.ApplyInsanity(ent.Owner, TimeSpan.FromSeconds(90));
        }

        // SS13 on_gain: немедленно конвертировать ~20% живого экипажа по всей станции
        var eligibleCrew = new List<EntityUid>();
        var crewQuery = EntityQueryEnumerator<MobStateComponent>();
        while (crewQuery.MoveNext(out var mobUid, out var mobState))
        {
            if (mobUid == uid) continue;
            if (!_mobs.IsAlive(mobUid, mobState)) continue;
            if (HasComp<HereticComponent>(mobUid)) continue;
            if (HasComp<HereticMoonConvertedComponent>(mobUid)) continue;
            if (HasComp<MindShieldComponent>(mobUid)) continue;
            eligibleCrew.Add(mobUid);
        }
        var convertCount = Math.Max(1, eligibleCrew.Count / 5);
        _random.Shuffle(eligibleCrew);
        for (var i = 0; i < Math.Min(convertCount, eligibleCrew.Count); i++)
        {
            var target = eligibleCrew[i];
            _moonAmulet.TryMoonConvert(target, 0f);
            Spawn("HereticMoonAmulet", Transform(target).Coordinates);
        }

        if (TryComp<HumanoidProfileComponent>(uid, out var ascHumanoid) &&
            _prototype.Resolve(ascHumanoid.Species, out var ascSpeciesProto))
        {
            var casterMeta = MetaData(uid);
            for (var i = 0; i < 5; i++)
            {
                var illusionAngle  = _random.NextFloat(0f, MathF.PI * 2f);
                var illusionDist   = _random.NextFloat(1f, 5f);
                var illusionOffset = new Vector2(MathF.Cos(illusionAngle) * illusionDist, MathF.Sin(illusionAngle) * illusionDist);
                var illusion = Spawn(ascSpeciesProto.Prototype, coords.Offset(illusionOffset));
                _visualBody.CopyAppearanceFrom(uid, illusion);
                var illusionCoords = Transform(illusion).Coordinates;
                var slotEnum = _inventory.GetSlotEnumerator(uid);
                while (slotEnum.NextItem(out var item, out var slot))
                {
                    var protoId = MetaData(item).EntityPrototype?.ID;
                    if (protoId == null) continue;
                    var copy = Spawn(protoId, illusionCoords);
                    _inventory.TryEquip(illusion, copy, slot.Name, silent: true);
                }
                RemComp<DamageableComponent>(illusion);
                RemComp<MindContainerComponent>(illusion);
                RemComp<SSDIndicatorComponent>(illusion);
                RemComp<InputMoverComponent>(illusion);
                RemComp<MobMoverComponent>(illusion);
                _visibility.SetLayer(illusion, (ushort) VisibilityFlags.HereticIllusion);
                EnsureComp<TimedDespawnComponent>(illusion).Lifetime = 30f;
                EnsureComp<HereticMoonIllusionComponent>(illusion);
                EnsureComp<HereticMoonIllusionMovementComponent>(illusion);
                EnsureComp<CanMoveInAirComponent>(illusion);
                _metaData.SetEntityName(illusion, casterMeta.EntityName);
                _metaData.SetEntityDescription(illusion, casterMeta.EntityDescription);
            }
        }
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Heretic/ascend_moon.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-moon"), uid, uid, PopupType.Large);
        EnsureComp<HereticMoonAscendedComponent>(uid);
    }
}
