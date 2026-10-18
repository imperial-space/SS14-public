using Content.Shared.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>Без дыхания (breathless): лёгкие не нужны, удушья нет.</summary>
[RegisterComponent]
public sealed partial class GeneticBreathlessComponent : Component;

/// <summary>Быстрота (quick): мышцы ног работают на 7.5 % эффективнее.</summary>
[RegisterComponent]
public sealed partial class GeneticQuickComponent : Component
{
    [DataField]
    public float SpeedModifier = 1.075f;
}

/// <summary>Ловкость (dextrous): удары без оружия вдвое чаще.</summary>
[RegisterComponent]
public sealed partial class GeneticDextrousComponent : Component
{
    [DataField]
    public float AttackRateMultiplier = 2f;
}

/// <summary>Огнеупорность (fire immunity): носитель не горит.</summary>
[RegisterComponent]
public sealed partial class GeneticFireproofComponent : Component;

/// <summary>Быстрое восстановление (quick recovery): оглушение вдвое короче.</summary>
[RegisterComponent]
public sealed partial class GeneticQuickRecoveryComponent : Component
{
    [DataField]
    public float StunMultiplier = 0.5f;
}

/// <summary>Плазмоцил (plasmocile): плазма в крови не отравляет.</summary>
[RegisterComponent]
public sealed partial class GeneticPlasmocileComponent : Component
{
    [DataField]
    public string Reagent = "Plasma";
}

/// <summary>Улучшения ДНК-хранилища (vault_mutation.dm).</summary>
public sealed class VaultMutationsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly RespiratorSystem _respirator = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GeneticQuickComponent, ComponentStartup>(OnQuickChanged);
        SubscribeLocalEvent<GeneticQuickComponent, ComponentShutdown>(OnQuickChanged);
        SubscribeLocalEvent<GeneticQuickComponent, RefreshMovementSpeedModifiersEvent>(OnQuickRefresh);
        SubscribeLocalEvent<GeneticDextrousComponent, GetMeleeAttackRateEvent>(OnDextrousAttackRate);
        SubscribeLocalEvent<GeneticQuickRecoveryComponent, StunnedEvent>(OnStunned);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

        var breathless = EntityQueryEnumerator<GeneticBreathlessComponent, RespiratorComponent>();
        while (breathless.MoveNext(out var uid, out _, out var respirator))
        {
            if (respirator.Saturation < respirator.MaxSaturation)
                _respirator.UpdateSaturation(uid, respirator.MaxSaturation - respirator.Saturation, respirator);
        }

        var fireproof = EntityQueryEnumerator<GeneticFireproofComponent, FlammableComponent>();
        while (fireproof.MoveNext(out var uid, out _, out var flammable))
        {
            if (flammable.OnFire || flammable.FireStacks > 0)
                _flammable.Extinguish(uid, flammable);
        }

        var plasmocile = EntityQueryEnumerator<GeneticPlasmocileComponent, BloodstreamComponent>();
        while (plasmocile.MoveNext(out var uid, out var comp, out var bloodstream))
        {
            if (!_solution.ResolveSolution(uid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var solution))
                continue;

            var amount = solution.GetTotalPrototypeQuantity(comp.Reagent);
            if (amount > 0)
                _solution.RemoveReagent(bloodstream.BloodSolution.Value, comp.Reagent, amount);
        }
    }

    private void OnQuickChanged<T>(Entity<GeneticQuickComponent> ent, ref T args)
    {
        if (!TerminatingOrDeleted(ent))
            _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnQuickRefresh(Entity<GeneticQuickComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.ModifySpeed(ent.Comp.SpeedModifier, ent.Comp.SpeedModifier);
    }

    /// <summary>Событие скорости атаки приходит на оружие, так что ловкость ускоряет удары без оружия.</summary>
    private void OnDextrousAttackRate(Entity<GeneticDextrousComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        args.Multipliers *= ent.Comp.AttackRateMultiplier;
    }

    private void OnStunned(Entity<GeneticQuickRecoveryComponent> ent, ref StunnedEvent args)
    {
        if (!_status.TryGetTime(ent, SharedStunSystem.StunId, out var time) || time.EndEffectTime is not { } end)
            return;

        var remaining = end - _timing.CurTime;
        if (remaining <= TimeSpan.Zero)
            return;

        _status.TrySetStatusEffectDuration(ent, SharedStunSystem.StunId, remaining * ent.Comp.StunMultiplier);
    }
}
