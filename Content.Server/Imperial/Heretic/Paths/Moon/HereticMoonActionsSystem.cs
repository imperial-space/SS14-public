using Content.Server.Body;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic.Paths.Moon;

/// <summary>
/// Способности пути Луны.
/// </summary>
public sealed class HereticMoonActionsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly HereticMoonAmuletSystem _moonAmulet = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly HereticMoonBrainDamageSystem _brainDamage = default!;
    [Dependency] private readonly VisualBodySystem _visualBody = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly HereticLunaticSystem _lunatic = default!;
    [Dependency] private readonly Content.Server.Mind.MindSystem _mind = default!;
    [Dependency] private readonly Content.Server.Imperial.Antimagic.ImperialAntimagicSystem _antimagic = default!;

    private static readonly DamageSpecifier ApplyMoonGateEffectsAsphyxiationDamage = new() { DamageDict = { ["Asphyxiation"] = 30 } };

    private static readonly TimeSpan ApplyMoonGateEffectsDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ApplyMoonGateEffectsTime = TimeSpan.FromSeconds(2);

    private const float ApplyMoonGateEffectsAmount = 20f;
    private const float ApplyMoonGateEffectsAmount2 = 30f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, HereticMoonGateActionEvent>(OnMoonGate);
        SubscribeLocalEvent<HereticPathAscendedEvent>(OnPathAscended);
    }

    private void OnMoonGate(EntityUid uid, HereticComponent comp, HereticMoonGateActionEvent args)
    {
        if (args.Handled)
            return;
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
        _brainDamage.AddBrainDamage(uid, ApplyMoonGateEffectsAmount, isCasterCapped: true);

        // SS13: cast_on.mob_mood.adjust_sanity(-20)
        if (!TryComp<HereticMoonBrainDamageComponent>(target, out var targetBrain))
            targetBrain = AddComp<HereticMoonBrainDamageComponent>(target);

        var targetSanity = targetBrain.Sanity;
        targetBrain.Sanity = Math.Max(0f, targetBrain.Sanity - 20f);

        // SS13: cast_on.adjust_oxy_loss(30)
        var oxyDmg = new DamageSpecifier(ApplyMoonGateEffectsAsphyxiationDamage);
        _damage.TryChangeDamage(target, oxyDmg, ignoreResistances: false);

        // SS13: cast_on.cause_hallucination(body) + cause_hallucination(heretic/gate)
        _hereticEffects.ApplyHallucination(target, ApplyMoonGateEffectsDuration);

        // SS13: cast_on.adjust_organ_loss(ORGAN_SLOT_BRAIN, 30)
        _brainDamage.AddBrainDamage(target, ApplyMoonGateEffectsAmount2, isCasterCapped: false);

        // SS13 duration formula: ((SANITY_MAXIMUM - sanity) / (SANITY_MAXIMUM - SANITY_INSANE)) * 15s + 1s
        // SANITY_MAXIMUM=150, SANITY_INSANE=0 → divisor = 150
        var durationSecs = Math.Clamp((150f - targetSanity) / 150f * 15f + 1f, 1f, 16f);
        var duration = TimeSpan.FromSeconds(durationSecs);

        // SS13: cast_on.adjust_temp_blindness(duration) + cast_on.adjust_silence(duration)
        _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(target, TemporaryBlindnessSystem.BlindingStatusEffect, duration, true);
        _statusEffects.TryAddStatusEffect<MutedComponent>(target, "Muted", duration, true);

        // SS13: cast_on.sound_damage(0, mind_gate_duration) — цель ещё и глохнет.
        _hereticEffects.ApplyDeafness(target, duration);

        // SS13: cast_on.adjust_confusion(10 SECONDS) — insanity is the closest SS14 analog
        _hereticEffects.ApplyInsanity(target, ApplyMoonGateEffectsDuration);

        // SS13: if(cast_on.mob_mood.sanity < 40) cast_on.AdjustKnockdown(2 SECONDS)
        if (targetSanity < 40f)
            _stun.TryKnockdown(target, ApplyMoonGateEffectsTime, true);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);

        // our_passive?.amulet: проводят либо надетый амулет, либо встроенный на 3 уровне пассивки.
        if (TryComp<HereticComponent>(uid, out var gateHeretic)
            && (gateHeretic.PassiveLevel >= 3 || HasComp<HereticMoonAmuletEquippedComponent>(uid)))
        {
            _moonAmulet.ChannelAmulet(uid, target);
        }
    }

    private void OnPathAscended(ref HereticPathAscendedEvent args)
    {
        if (args.Component.CurrentPath != HereticPath.Moon)
            return;

        var uid = args.Heretic;
        // ultimate/moon_final/on_finished_recipe(): еретик становится Постановщиком.
        _lunatic.MakeMaster(uid);

        // Примерно пятая часть станции встаёт лунатиками на сторону еретика.
        var candidates = new List<EntityUid>();
        var crewQuery = EntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent>();
        while (crewQuery.MoveNext(out var mobUid, out _, out var mobState))
        {
            if (mobUid == uid)
                continue;
            if (!_mobs.IsAlive(mobUid, mobState))
                continue;
            if (!_mind.TryGetMind(mobUid, out _, out var mobMind) || mobMind.UserId == null)
                continue;
            if (_antimagic.CanBlockMagic(mobUid))
                continue;
            if (Transform(mobUid).MapID != Transform(uid).MapID)
                continue;
            candidates.Add(mobUid);
        }

        _random.Shuffle(candidates);
        var maxLunatics = (int)Math.Ceiling(candidates.Count * 0.2);
        var lunatics = 0;
        foreach (var crewmate in candidates)
        {
            if (lunatics > maxLunatics)
            {
                _popup.PopupEntity(Loc.GetString("lunatic-convert-uneasy"), crewmate, crewmate, PopupType.MediumCaution);
                continue;
            }

            if (_lunatic.TryConvertWithAmulet(crewmate, uid))
                lunatics++;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Heretic/ascend_moon.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-moon"), uid, uid, PopupType.Large);
        EnsureComp<HereticMoonAscendedComponent>(uid);
    }
}
