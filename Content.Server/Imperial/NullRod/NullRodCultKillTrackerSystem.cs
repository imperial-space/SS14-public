using Content.Shared.Examine;
using Content.Shared.Imperial.Chaplain.Components;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Imperial.NullRod.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Imperial.NullRod;

/// <summary>
/// Запоминает культистов, которых святой вывел из строя или убил этим оружием (cult_kill_tracker из SS13).
/// Состояние цели сравнивается до удара и на следующем тике, когда урон уже нанесён.
/// </summary>
public sealed class NullRodCultKillTrackerSystem : EntitySystem
{
    private readonly List<(EntityUid Weapon, EntityUid Target, MobState Before)> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullRodCultKillTrackerComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<NullRodCultKillTrackerComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        foreach (var (weapon, target, before) in _pending)
        {
            if (!TryComp<NullRodCultKillTrackerComponent>(weapon, out var tracker))
                continue;

            // Удалённая цель, скорее всего, мертва.
            if (TerminatingOrDeleted(target)
                || TryComp<MobStateComponent>(target, out var mobState) && mobState.CurrentState > before)
            {
                tracker.Slain.Add(target);
            }
        }

        _pending.Clear();
    }

    private void OnMeleeHit(Entity<NullRodCultKillTrackerComponent> weapon, ref MeleeHitEvent args)
    {
        if (!args.IsHit || !HasComp<ImperialHolyComponent>(args.User))
            return;

        foreach (var target in args.HitEntities)
        {
            if (!HasComp<CultistComponent>(target) || !TryComp<MobStateComponent>(target, out var mobState))
                continue;

            if (mobState.CurrentState != MobState.Dead)
                _pending.Add((weapon, target, mobState.CurrentState));
        }
    }

    /// <summary>Кровь павших культистов видят только сами культисты.</summary>
    private void OnExamined(Entity<NullRodCultKillTrackerComponent> weapon, ref ExaminedEvent args)
    {
        if (!HasComp<CultistComponent>(args.Examiner) || weapon.Comp.Slain.Count == 0)
            return;

        args.PushMarkup(Loc.GetString("null-rod-cult-kills", ("count", weapon.Comp.Slain.Count)));
    }
}
