using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Mobs.Components;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

public sealed partial class DeathNoteGuidedScenarioSystem
{
    private void UpdateDisposalVictim(EntityUid uid, DeathNoteDisposalVictimComponent victim)
    {
        if (victim.ImpactsRemaining <= 0 ||
            TryComp(uid, out MobStateComponent? mobState) && _mobState.IsDead(uid, mobState))
        {
            RemCompDeferred<DeathNoteDisposalVictimComponent>(uid);
            return;
        }

        // При просадке тиков воспроизводим все уже наступившие удары, иначе последний
        // из них мог потеряться на границе срока действия и оставить цель в крите.
        while (victim.ImpactsRemaining > 0 && _timing.CurTime >= victim.NextImpact)
        {
            _damageable.TryChangeDamage(
                uid,
                new Content.Shared.Damage.DamageSpecifier(victim.DamagePerImpact),
                ignoreResistances: true,
                interruptsDoAfters: true,
                origin: Deleted(victim.DisposalUnit) ? null : victim.DisposalUnit,
                ignoreGlobalModifiers: true);
            victim.ImpactsRemaining--;
            victim.NextImpact += victim.ImpactInterval;

            if (TryComp(uid, out mobState) && _mobState.IsDead(uid, mobState))
                break;
        }

        if (victim.ImpactsRemaining <= 0 ||
            _timing.CurTime >= victim.ExpiresAt ||
            TryComp(uid, out mobState) && _mobState.IsDead(uid, mobState))
        {
            RemCompDeferred<DeathNoteDisposalVictimComponent>(uid);
        }
    }
}
