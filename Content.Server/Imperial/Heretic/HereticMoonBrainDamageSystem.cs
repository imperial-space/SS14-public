using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticMoonBrainDamageSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;

    private static readonly EntProtoId HallucinationId = "HereticWeeepingHallucinationStatusEffect";

    /// <summary>
    /// Наносит урон по мозгу еретика Пути Луны.
    /// isCasterCapped=true — кастер, урон ограничен CasterCap=140.
    /// </summary>
    public void AddBrainDamage(EntityUid target, float amount, bool isCasterCapped = false)
    {
        if (!TryComp<HereticMoonBrainDamageComponent>(target, out var comp))
            comp = AddComp<HereticMoonBrainDamageComponent>(target);

        // Level 1: Moon-еретик защищён от смерти мозга и галлюцинаций
        var isMoonProtected = TryComp<HereticComponent>(target, out var heretic) &&
                              heretic.CurrentPath == HereticPath.Moon &&
                              heretic.PassiveLevel >= 1;
        if (isMoonProtected)
            isCasterCapped = true;

        var cap = isCasterCapped ? HereticMoonBrainDamageComponent.CasterCap : HereticMoonBrainDamageComponent.MaxBrainDamage;
        var previous = comp.BrainDamage;
        comp.BrainDamage = Math.Min(comp.BrainDamage + amount, cap);

        // Порог 45 — лёгкие симптомы
        if (!comp.LowThresholdMessageSent && comp.BrainDamage >= HereticMoonBrainDamageComponent.LowThreshold)
        {
            comp.LowThresholdMessageSent = true;
            _popup.PopupEntity(Loc.GetString("heretic-moon-brain-low"), target, target, PopupType.MediumCaution);
        }

        // Порог 120 — тяжёлые симптомы
        if (!comp.HighThresholdMessageSent && comp.BrainDamage >= HereticMoonBrainDamageComponent.HighThreshold)
        {
            comp.HighThresholdMessageSent = true;
            _popup.PopupEntity(Loc.GetString("heretic-moon-brain-high"), target, target, PopupType.LargeCaution);
        }

        // Порог 200 — смерть мозга (только для цели, не кастера)
        if (!isCasterCapped && comp.BrainDamage >= HereticMoonBrainDamageComponent.MaxBrainDamage)
        {
            _popup.PopupEntity(Loc.GetString("heretic-moon-brain-death"), target, target, PopupType.LargeCaution);
            var deathDmg = new DamageSpecifier();
            deathDmg.DamageDict["Cellular"] = FixedPoint2.New(200);
            _damage.TryChangeDamage(target, deathDmg, ignoreResistances: true);
        }

        // Галлюцинации при высоком уроне мозгу (порог 120), только если нет Moon-защиты
        if (!isMoonProtected && comp.BrainDamage >= HereticMoonBrainDamageComponent.HighThreshold)
        {
            _statusEffects.TryAddStatusEffectDuration(target, HallucinationId, TimeSpan.FromSeconds(300));
        }
    }
}
