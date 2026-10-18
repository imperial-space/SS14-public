using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Items;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Imperial.Heretic.Paths.Moon.Parade;
using Content.Server.Imperial.Heretic.Paths.Void;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic.Paths.Moon;

public sealed class HereticMoonBrainDamageSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private static readonly EntProtoId HallucinationId = "HereticWeeepingHallucinationStatusEffect";

    /// <summary>
    /// Наносит урон по мозгу еретика Пути Луны.
    /// isCasterCapped=true — кастер, урон ограничен CasterCap=140.
    /// ignoreMoonProtection=true — пассивка Луны не спасает: так работает Блистательное облачение,
    /// в котором смерть мозга обязана добивать носителя.
    /// </summary>
    public void AddBrainDamage(EntityUid target, float amount, bool isCasterCapped = false, bool ignoreMoonProtection = false)
    {
        if (!TryComp<HereticMoonBrainDamageComponent>(target, out var comp))
            comp = AddComp<HereticMoonBrainDamageComponent>(target);

        // Level 1: Moon-еретик защищён от смерти мозга и галлюцинаций
        var isMoonProtected = !ignoreMoonProtection &&
                              TryComp<HereticComponent>(target, out var heretic) &&
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

        // Порог 200 — смерть мозга (только для цели, не кастера).
        // TRAIT_BRAIN_DAMAGE_NODEATH: в Блистательном облачении добивает сама роба,
        // иначе её же конвертация урона загонит нас в рекурсию.
        if (!isCasterCapped
            && !comp.DeathTriggered
            && !HasComp<HereticGlitteringArrayEquippedComponent>(target)
            && comp.BrainDamage >= HereticMoonBrainDamageComponent.MaxBrainDamage)
        {
            comp.DeathTriggered = true;
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
