using Content.Server.Imperial.Heretic;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Items;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Imperial.Heretic.Paths.Moon.Parade;
using Content.Server.Imperial.Heretic.Paths.Void;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Inventory.Events;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Components;
using Content.Server.Popups;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Heretic.Paths.Moon;

public sealed class HereticAuraSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem          _damage        = default!;
    [Dependency] private readonly EntityLookupSystem        _lookup        = default!;
    [Dependency] private readonly HereticSystem             _heretic       = default!;
    [Dependency] private readonly HereticMoonAmuletSystem    _moonAmulet     = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly PopupSystem                _popup          = default!;
    [Dependency] private readonly SharedAudioSystem          _audio          = default!;
    [Dependency] private readonly SharedStunSystem           _stun           = default!;
    [Dependency] private readonly StatusEffectsSystem        _statusEffects  = default!;
    [Dependency] private readonly GibbingSystem              _gibbing        = default!;
    [Dependency] private readonly HereticLunaticSystem       _lunatic        = default!;
    [Dependency] private readonly MobStateSystem             _mobs           = default!;
    [Dependency] private readonly IRobustRandom              _random         = default!;
    [Dependency] private readonly IGameTiming                _timing         = default!;
    [Dependency] private readonly Content.Server.Imperial.Antimagic.ImperialAntimagicSystem _antimagic = default!;

    private const float AuraTickInterval = 5f;
    private const float AuraRadius       = 4f;
    private const float MoonAuraRadius   = 7f;

    /// <summary>on_life(): carbon_sanity < 10 — рассудок сломан окончательно.</summary>
    private const float MoonInsaneSanity = 10f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, BeforeStaminaDamageEvent>(OnBeforeStaminaDamage);
        SubscribeLocalEvent<HereticComponent, DidEquipEvent>(OnHereticEquipped);
        SubscribeLocalEvent<HereticComponent, DidUnequipEvent>(OnHereticUnequipped);
    }

    private void OnBeforeStaminaDamage(Entity<HereticComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (args.Cancelled) return;
        if (!_heretic.HasKnowledge(ent.Comp, "KnowledgeLeechingWalk")) return;
        if (!_heretic.IsTileRusted(Transform(ent).Coordinates)) return;

        args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_heretic.TryTickAuraAccumulator(uid, comp, frameTime, AuraTickInterval)) continue;
            TickAura(uid, comp);
            TickLeechingWalk(uid, comp);
        }

        // У Луны аура на своём такте — раз в Life() вознёсшегося.
        var now = _timing.CurTime;
        var moonQuery = EntityQueryEnumerator<HereticMoonAscendedComponent>();
        while (moonQuery.MoveNext(out var uid, out var moon))
        {
            if (now < moon.NextAuraTick)
                continue;
            moon.NextAuraTick = now + moon.AuraInterval;

            if (_mobs.IsDead(uid))
                continue;

            TickMoonAscensionAura(uid);
        }
    }

    private void TickAura(EntityUid uid, HereticComponent comp)
    {
        // Аура Луны тикает отдельно, см. Update.
        if (comp.CurrentPath == HereticPath.Moon)
            return;

        var dmg = GetAuraDamage(comp.CurrentPath);
        if (dmg == null) return;

        var coords = Transform(uid).Coordinates;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, AuraRadius))
        {
            if (ent.Owner == uid) continue;
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
        }
    }

    /// <summary>
    /// ultimate/moon_final/on_life(): радиус 7, галлюцинации, +2с замешательства, −20 рассудка.
    /// Сломанный рассудок (&lt; 10): слепота 5с, защищённым разумом и культистам сносит голову,
    /// остальных обращает в лунатиков.
    /// </summary>
    private void TickMoonAscensionAura(EntityUid uid)
    {
        var coords = Transform(uid).Coordinates;

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, MoonAuraRadius))
        {
            if (ent.Owner == uid) continue;

            // visible_hallucination_pulse(radius = 7, 60 SECONDS) задевает всех.
            _hereticEffects.ApplyHallucination(ent.Owner, TimeSpan.FromSeconds(60));

            if (!_mobs.IsAlive(ent.Owner)) continue;
            if (!HasComp<HumanoidProfileComponent>(ent.Owner)) continue;
            if (HasComp<HereticComponent>(ent.Owner)) continue;
            if (_antimagic.CanBlockMagic(ent.Owner)) continue;

            Spawn("HereticEffectRingleader", Transform(ent.Owner).Coordinates);

            var brainComp = EnsureComp<HereticMoonBrainDamageComponent>(ent.Owner);
            var sanity = brainComp.Sanity;

            _hereticEffects.ApplyInsanity(ent.Owner, TimeSpan.FromSeconds(2));
            brainComp.Sanity = Math.Max(0f, brainComp.Sanity - 20f);

            if (sanity >= MoonInsaneSanity) continue;

            if (_random.Prob(0.2f))
                _popup.PopupEntity(Loc.GetString("heretic-moon-ascension-echo"), ent.Owner, ent.Owner, PopupType.SmallCaution);

            _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(
                ent.Owner,
                TemporaryBlindnessSystem.BlindingStatusEffect,
                TimeSpan.FromSeconds(5),
                true);

            // should_mind_explode(): имплант защиты разума или кровавая магия — голова не выдерживает.
            if (HasComp<MindShieldComponent>(ent.Owner) || HasComp<CultistComponent>(ent.Owner))
            {
                _popup.PopupEntity(Loc.GetString("heretic-moon-ascension-mind-explode"), ent.Owner, ent.Owner, PopupType.LargeCaution);
                _gibbing.Gib(ent.Owner);
            }
            else
            {
                _lunatic.TryConvertWithAmulet(ent.Owner, uid);
            }
        }
    }

    private DamageSpecifier? GetAuraDamage(HereticPath path)
    {
        return path switch
        {
            HereticPath.Rust => MakeDmg("Poison", 2),
            HereticPath.Void => MakeDmg("Cold", 2),
            _                => null,
        };
    }

    private static DamageSpecifier MakeDmg(string type, int amount)
    {
        var spec = new DamageSpecifier();
        spec.DamageDict[type] = FixedPoint2.New(amount);
        return spec;
    }

    private void TickLeechingWalk(EntityUid uid, HereticComponent comp)
    {
        if (!_heretic.HasKnowledge(comp, "KnowledgeLeechingWalk")) return;
        if (!_heretic.IsTileRusted(Transform(uid).Coordinates)) return;

        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"] = FixedPoint2.New(-5);
        heal.DamageDict["Slash"] = FixedPoint2.New(-5);
        _damage.TryChangeDamage(uid, heal, ignoreResistances: true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_ambience_antag_heretic_heretic_gain.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-leeching-walk-heal"), uid, uid, PopupType.Small);
    }

    private void OnHereticEquipped(Entity<HereticComponent> ent, ref DidEquipEvent args)
    {
        if (!HasComp<HereticPathRobeComponent>(args.Equipment))
            return;
        var visComp = EnsureComp<HereticAuraVisualsComponent>(ent.Owner);
        visComp.IsWearingRobe = true;
        _heretic.HideHereticAura(visComp);
    }

    private void OnHereticUnequipped(Entity<HereticComponent> ent, ref DidUnequipEvent args)
    {
        if (!HasComp<HereticPathRobeComponent>(args.Equipment))
            return;
        if (!TryComp<HereticAuraVisualsComponent>(ent.Owner, out var visComp))
            return;
        visComp.IsWearingRobe = false;
        if (visComp.HasEarnedAura)
            _heretic.ShowHereticAura(ent.Owner, visComp);
    }
}
