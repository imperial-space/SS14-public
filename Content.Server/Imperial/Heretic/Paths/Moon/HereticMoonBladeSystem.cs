using Content.Server.Imperial.Heretic;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Items;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Imperial.Heretic.Paths.Moon.Parade;
using Content.Server.Imperial.Heretic.Paths.Void;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic.Paths.Moon;

/// <summary>
/// Лунный клинок: подрыв метки Луны, улучшение blade_upgrade/moon и пацификация под лунным амулетом.
/// </summary>
public sealed class HereticMoonBladeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem            _audio          = default!;
    [Dependency] private readonly HereticMarkSystem _marks = default!;
    [Dependency] private readonly HereticMoonAmuletSystem      _amulet         = default!;
    [Dependency] private readonly HereticMoonBrainDamageSystem _brainDamage    = default!;
    [Dependency] private readonly HereticStatusEffectsSystem   _hereticEffects = default!;
    [Dependency] private readonly HereticSystem                _heretic        = default!;
    [Dependency] private readonly MobStateSystem               _mobs           = default!;
    [Dependency] private readonly SharedPopupSystem            _popup          = default!;
    [Dependency] private readonly IRobustRandom                _random         = default!;

    /// <summary>SANITY_NEUTRAL: выше него клинок бьёт мозг слабее.</summary>
    private const float NeutralSanity = 100f;

    private const float SanityDamage = 10f;
    private const float BrainDamageAwake = 10f;
    private const float BrainDamageBroken = 25f;

    /// <summary>eldritch/moon/on_effect(): adjust_organ_loss(ORGAN_SLOT_BRAIN, 25, 160).</summary>
    private const float MarkBrainDamage = 25f;

    /// <summary>eldritch/moon/on_damaged(): после 15 урона цель снова может драться.</summary>
    private const float MarkPacifyBreak = 15f;

    private static readonly SoundPathSpecifier[] LaughSounds =
    [
        new("/Audio/Voice/Human/manlaugh1.ogg"),
        new("/Audio/Voice/Human/manlaugh2.ogg"),
        new("/Audio/Voice/Human/womanlaugh.ogg"),
    ];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMoonBladeComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<MoonMarkComponent, ComponentStartup>(OnMarkStartup);
        SubscribeLocalEvent<MoonMarkComponent, ComponentShutdown>(OnMarkShutdown);
        SubscribeLocalEvent<MoonMarkComponent, DamageChangedEvent>(OnMarkDamaged);
    }

    #region Метка Луны

    /// <summary>eldritch/moon/on_apply(): цель «и мышке не навредит».</summary>
    private void OnMarkStartup(Entity<MoonMarkComponent> ent, ref ComponentStartup args)
    {
        _marks.Refresh(ent.Comp);

        if (!HasComp<PacifiedComponent>(ent))
        {
            EnsureComp<PacifiedComponent>(ent);
            ent.Comp.AddedPacified = true;
        }

        Laugh(ent);
        _popup.PopupEntity(Loc.GetString("heretic-moon-mark-pacified"), ent, ent, PopupType.SmallCaution);
    }

    private void OnMarkShutdown(Entity<MoonMarkComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.AddedPacified && !TerminatingOrDeleted(ent))
            RemComp<PacifiedComponent>(ent);
    }

    private void OnMarkDamaged(Entity<MoonMarkComponent> ent, ref DamageChangedEvent args)
    {
        if (!ent.Comp.AddedPacified || !args.DamageIncreased || args.DamageDelta == null)
            return;

        foreach (var value in args.DamageDelta.DamageDict.Values)
        {
            if (value > FixedPoint2.Zero)
                ent.Comp.DamageSustained += value.Float();
        }

        if (ent.Comp.DamageSustained < MarkPacifyBreak)
            return;

        ent.Comp.AddedPacified = false;
        RemComp<PacifiedComponent>(ent);
        _popup.PopupEntity(Loc.GetString("heretic-moon-mark-fight-again"), ent, ent, PopupType.SmallCaution);
    }

    /// <summary>eldritch/moon/on_effect(): подрыв метки клинком.</summary>
    private void DetonateMark(EntityUid target)
    {
        _hereticEffects.ApplyInsanity(target, TimeSpan.FromSeconds(30));
        _brainDamage.AddBrainDamage(target, MarkBrainDamage);
        Laugh(target);
        RemComp<MoonMarkComponent>(target);
    }

    #endregion

    private void OnMeleeHit(EntityUid uid, HereticMoonBladeComponent comp, MeleeHitEvent args)
    {
        if (!TryComp<HereticComponent>(args.User, out var heretic))
            return;

        // on_equip_item(): в амулете клинок теряет force целиком, оставляя только эффекты.
        var pacified = HasComp<HereticMoonAmuletEquippedComponent>(args.User);
        if (pacified)
            args.ModifiersList.Add(ZeroAll(args.BaseDamage));

        if (!args.IsHit)
            return;

        var upgraded = _heretic.HasKnowledge(heretic, "KnowledgeMoonBlade");

        foreach (var target in args.HitEntities)
        {
            if (target == args.User)
                continue;
            if (HasComp<HereticComponent>(target))
                continue;
            if (!HasComp<MobStateComponent>(target))
                continue;

            if (HasComp<MoonMarkComponent>(target))
                DetonateMark(target);

            // blade_upgrade/moon/do_melee_effects()
            if (upgraded)
                DoUpgradeEffects(target);

            // COMSIG_HERETIC_BLADE_ATTACK: в амулете удар клинком проводит сам амулет.
            if (pacified)
                _amulet.ChannelAmulet(args.User, target);
        }
    }

    private void DoUpgradeEffects(EntityUid target)
    {
        var brainComp = EnsureComp<HereticMoonBrainDamageComponent>(target);

        _hereticEffects.ApplyHallucination(target, TimeSpan.FromSeconds(10));
        Laugh(target);
        brainComp.Sanity = Math.Max(0f, brainComp.Sanity - SanityDamage);

        // В сознании и с целым рассудком мозгу достаётся меньше.
        var awake = _mobs.IsAlive(target) && brainComp.Sanity >= NeutralSanity;
        _brainDamage.AddBrainDamage(target, awake ? BrainDamageAwake : BrainDamageBroken);
    }

    /// <summary>emote(pick("giggle", "laugh")) — смеётся жертва.</summary>
    private void Laugh(EntityUid target)
    {
        _audio.PlayPvs(_random.Pick(LaughSounds), target);
    }

    /// <summary>Модификатор, обнуляющий весь физический урон клинка.</summary>
    private static DamageModifierSet ZeroAll(DamageSpecifier damage)
    {
        var set = new DamageModifierSet();
        foreach (var (key, value) in damage.DamageDict)
        {
            if (value > FixedPoint2.Zero)
                set.Coefficients[key] = 0f;
        }

        return set;
    }
}
