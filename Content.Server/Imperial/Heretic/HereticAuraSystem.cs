using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Components;
using Content.Server.Popups;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Heretic;

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

    private const float AuraTickInterval = 5f;
    private const float AuraRadius       = 4f;
    private const float MoonAuraRadius   = 7f;

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
    }

    private void TickAura(EntityUid uid, HereticComponent comp)
    {
        // Moon возвышение — особая аура: санити-дрейн + конвертация
        if (comp.CurrentPath == HereticPath.Moon)
        {
            if (comp.AscensionTriggered)
                TickMoonAscensionAura(uid);
            return;
        }

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
    /// SS13 Moon ascension on_life: radius 7, -20 sanity/tick, 2s confusion.
    /// При sanity &lt; 10: MindShield → стан 30с, иначе → moon_converted.
    /// </summary>
    private void TickMoonAscensionAura(EntityUid uid)
    {
        var coords = Transform(uid).Coordinates;

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, MoonAuraRadius))
        {
            if (ent.Owner == uid) continue;
            if (HasComp<HereticComponent>(ent.Owner)) continue;

            // -20 санити каждый тик
            var brainComp = EnsureComp<HereticMoonBrainDamageComponent>(ent.Owner);
            brainComp.Sanity = Math.Max(0f, brainComp.Sanity - 20f);

            // 2 секунды галлюцинаций/замешательства
            _hereticEffects.ApplyHallucination(ent.Owner, TimeSpan.FromSeconds(2));

            if (brainComp.Sanity >= 10f) continue;

            // Цель достигла безумия (INSANE)
            if (HasComp<MindShieldComponent>(ent.Owner))
            {
                // SS13: head explosion → SS14: сильный стан
                _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(30), true);
            }
            else
            {
                // SS13: moon_converted — последователь еретика
                _moonAmulet.TryMoonConvert(ent.Owner, brainComp.Sanity);
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
