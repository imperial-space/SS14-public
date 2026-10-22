using Content.Server.Imperial.Antimagic;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Flash;
using Content.Shared.Imperial.Antimagic;
using Content.Shared.Jittering;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using NewStatus = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.Server.Imperial.Cult;

/// <summary>Статусы SS13 (Paralyze, Unconscious, silence, stutter, slur, jitter) для культа.</summary>
public sealed partial class CultSystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StatusEffectsSystem _oldStatus = default!;
    [Dependency] private readonly NewStatus _status = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly ImperialAntimagicSystem _antimagic = default!;

    private static readonly EntProtoId StutterEffect = "StatusEffectStutter";
    private static readonly EntProtoId SlurEffect = "StatusEffectSlurred";
    private static readonly EntProtoId SleepEffect = "StatusEffectForcedSleeping";

    public void Paralyze(EntityUid uid, float seconds)
    {
        if (seconds > 0)
            _stun.TryAddParalyzeDuration(uid, TimeSpan.FromSeconds(seconds));
    }

    /// <summary>Unconscious().</summary>
    public void Unconscious(EntityUid uid, float seconds)
    {
        _status.TryAddStatusEffectDuration(uid, SleepEffect, TimeSpan.FromSeconds(seconds));
    }

    /// <summary>status_effect/silenced.</summary>
    public void Silence(EntityUid uid, float seconds)
    {
        _oldStatus.TryAddStatusEffect<MutedComponent>(uid, "Muted", TimeSpan.FromSeconds(seconds), true);
    }

    public void Stutter(EntityUid uid, float seconds)
    {
        _status.TryAddStatusEffectDuration(uid, StutterEffect, TimeSpan.FromSeconds(seconds));
    }

    /// <summary>speech/slurring/cult.</summary>
    public void CultSlur(EntityUid uid, float seconds)
    {
        _status.TryAddStatusEffectDuration(uid, SlurEffect, TimeSpan.FromSeconds(seconds));
    }

    public bool HasCultSlur(EntityUid uid) => _status.HasStatusEffect(uid, SlurEffect);

    public void Jitter(EntityUid uid, float seconds)
    {
        _jitter.DoJitter(uid, TimeSpan.FromSeconds(seconds), true);
    }

    public void Flash(EntityUid uid, EntityUid? user)
    {
        _flash.Flash(uid, user, null, TimeSpan.FromSeconds(2), 0.8f, displayPopup: false);
    }

    /// <summary>Снять немоту, заикание и культовую невнятность (руна обращения).</summary>
    public void ClearSpeechEffects(EntityUid uid)
    {
        _oldStatus.TryRemoveStatusEffect(uid, "Muted");
        _status.TryRemoveStatusEffect(uid, StutterEffect);
        _status.TryRemoveStatusEffect(uid, SlurEffect);
    }

    /// <summary>can_block_magic().</summary>
    public bool BlocksMagic(EntityUid uid, bool holy = false)
    {
        var flags = holy ? ImperialMagicResistance.Magic | ImperialMagicResistance.Holy : ImperialMagicResistance.Magic;
        return _antimagic.CanBlockMagic(uid, flags);
    }

    public float GetGroupDamage(EntityUid uid, string group)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return 0;
        return _damageable.GetAllDamage((uid, damageable)).GetDamagePerGroup(_proto).TryGetValue(group, out var v) ? v.Float() : 0;
    }

    /// <summary>adjust*Loss(-amount): лечение группы урона.</summary>
    public void HealGroup(EntityUid uid, string group, float amount)
    {
        if (amount <= 0 || !TryComp<DamageableComponent>(uid, out var damageable))
            return;
        _damageable.HealDistributed((uid, damageable), -FixedPoint2.New(amount), group);
    }

    /// <summary>take_overall_damage(brute, burn).</summary>
    public void OverallDamage(EntityUid uid, float brute, float burn, EntityUid? origin = null)
    {
        var spec = new DamageSpecifier();
        if (brute > 0)
            spec.DamageDict["Blunt"] = brute;
        if (burn > 0)
            spec.DamageDict["Heat"] = burn;
        _damageable.TryChangeDamage(uid, spec, origin: origin);
    }
}
