using Content.Shared.Clumsy;
using Content.Shared.Damage.Systems;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>
/// melee/baton/telescopic: в разложенном виде обычный удар (ЛКМ) не ранит, а сбивает с ног
/// и выматывает раз в 4 секунды; широкий замах (ПКМ) бьёт на 10 урона.
/// </summary>
public sealed class TelescopicBatonSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TelescopicBatonComponent, MeleeHitEvent>(OnMeleeHit);
    }

    /// <summary>baton/melee_attack_chain(): ЛКМ — оглушение, ПКМ — удар на вред.</summary>
    private void OnMeleeHit(Entity<TelescopicBatonComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.Direction != null)
            return;

        if (!TryComp<ItemToggleComponent>(ent, out var toggle) || !toggle.Activated)
            return;

        // Попытка оглушения не ранит.
        args.BonusDamage = -args.BaseDamage;

        if (_timing.CurTime < ent.Comp.NextStun)
            return;

        var user = args.User;

        // clumsy_check(): неуклюжий в половине случаев бьёт себя.
        if (HasComp<ClumsyComponent>(user) && _random.Prob(0.5f))
        {
            ent.Comp.NextStun = _timing.CurTime + ent.Comp.Cooldown;
            _popup.PopupEntity(Loc.GetString("telebaton-clumsy", ("user", user), ("item", ent.Owner)), user, PopupType.MediumCaution);
            _stun.TryKnockdown(user, ent.Comp.ClumsyKnockdownTime, true);
            _stamina.TakeStaminaDamage(user, ent.Comp.StaminaDamage, source: user, with: ent);
            _audio.PlayPvs(ent.Comp.StunSound, ent);
            return;
        }

        foreach (var target in args.HitEntities)
        {
            if (target == user || !HasComp<MobStateComponent>(target))
                continue;

            ent.Comp.NextStun = _timing.CurTime + ent.Comp.Cooldown;
            _stamina.TakeStaminaDamage(target, ent.Comp.StaminaDamage, source: user, with: ent);
            _stun.TryKnockdown(target, ent.Comp.KnockdownTime, true);
            _audio.PlayPvs(ent.Comp.StunSound, target);
            _popup.PopupEntity(Loc.GetString("telebaton-stun-others", ("user", user), ("target", target)), target,
                Robust.Shared.Player.Filter.PvsExcept(target), true, PopupType.MediumCaution);
            _popup.PopupEntity(Loc.GetString("telebaton-stun-target", ("user", user)), target, target, PopupType.LargeCaution);
            break;
        }
    }
}
