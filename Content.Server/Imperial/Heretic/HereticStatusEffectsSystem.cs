using Content.Server.Popups;
using Content.Server.Temperature.Systems;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticStatusEffectsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    public static readonly EntProtoId InsanityId = "HereticInsanityStatusEffect";
    public static readonly EntProtoId VoidChillId = "VoidChillStatusEffect";

    public override void Initialize()
    {
        SubscribeLocalEvent<HereticInsanityStatusEffectComponent, StatusEffectAppliedEvent>(OnInsanityApplied);
        SubscribeLocalEvent<VoidChillStatusEffectComponent, StatusEffectAppliedEvent>(OnVoidChillApplied);
        SubscribeLocalEvent<VoidChillStatusEffectComponent, StatusEffectRemovedEvent>(OnVoidChillRemoved);
    }

    private void OnInsanityApplied(Entity<HereticInsanityStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        ent.Comp.NextJitterTime = _timing.CurTime + TimeSpan.FromSeconds(
            _random.NextFloat(ent.Comp.JitterIntervalMin, ent.Comp.JitterIntervalMax));
    }

    private void OnVoidChillApplied(Entity<VoidChillStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        ent.Comp.NextTickTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.TickInterval);
    }

    private void OnVoidChillRemoved(Entity<VoidChillStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemCompDeferred<VoidChillComponent>(args.Target);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var insanityQuery = EntityQueryEnumerator<HereticInsanityStatusEffectComponent, StatusEffectComponent>();
        while (insanityQuery.MoveNext(out _, out var insanity, out var statusEffect))
        {
            if (_timing.CurTime < insanity.NextJitterTime)
                continue;
            if (statusEffect.AppliedTo is not { } target)
                continue;

            insanity.NextJitterTime = _timing.CurTime + TimeSpan.FromSeconds(
                _random.NextFloat(insanity.JitterIntervalMin, insanity.JitterIntervalMax));

            _stun.TryAddStunDuration(target, TimeSpan.FromSeconds(1));
            _popup.PopupEntity(Loc.GetString("heretic-insanity-jitter"), target, target, PopupType.SmallCaution);
        }

        var chillQuery = EntityQueryEnumerator<VoidChillStatusEffectComponent, StatusEffectComponent>();
        while (chillQuery.MoveNext(out _, out var chill, out var statusEffect))
        {
            if (_timing.CurTime < chill.NextTickTime)
                continue;
            if (statusEffect.AppliedTo is not { } target)
                continue;

            chill.NextTickTime = _timing.CurTime + TimeSpan.FromSeconds(chill.TickInterval);

            if (TryComp<TemperatureComponent>(target, out var temp))
            {
                var newTemp = temp.CurrentTemperature - 2f * chill.Stacks;
                _temperature.ForceChangeTemperature(target, newTemp, temp);
            }
        }
    }

    /// <summary>
    /// Applies Heretic Insanity to the target (stacks duration, max 60s).
    /// </summary>
    public void ApplyInsanity(EntityUid target, TimeSpan duration)
    {
        _statusEffects.TryAddStatusEffectDuration(target, InsanityId, duration);
    }

    /// <summary>
    /// Applies stacks of Void Chill to the target. Resets duration to 30s, accumulates stacks up to MaxStacks.
    /// </summary>
    public void ApplyVoidChill(EntityUid target, int stacks = 1)
    {
        if (!_statusEffects.TrySetStatusEffectDuration(target, VoidChillId, out var effectUid, TimeSpan.FromSeconds(20)))
            return;

        if (!TryComp<VoidChillStatusEffectComponent>(effectUid, out var chill))
            return;

        chill.Stacks = Math.Min(chill.Stacks + stacks, VoidChillStatusEffectComponent.MaxStacks);

        // 5 stacks = 0.60 speed (40% slow); 1 stack = 0.92 (8% slow)
        var mod = Math.Max(0.6f, 1f - 0.08f * chill.Stacks);
        _movementMod.TryUpdateMovementStatus(target, effectUid.Value, mod, mod);

        var visual = EnsureComp<VoidChillComponent>(target);
        visual.Stacks = chill.Stacks;
        Dirty(target, visual);
    }

    public static readonly EntProtoId VoidPrisonId = "VoidPrisonStatusEffect";

    /// <summary>
    /// Applies Void Prison to the target.
    /// </summary>
    public void ApplyVoidPrison(EntityUid target, TimeSpan duration)
    {
        _statusEffects.TryAddStatusEffectDuration(target, VoidPrisonId, duration);
    }

    public static readonly EntProtoId StarMarkId = "StarMarkStatusEffect";

    /// <summary>
    /// Applies Star Mark to the target (stacks duration).
    /// </summary>
    public void AddStarMark(EntityUid target, TimeSpan duration)
    {
        _statusEffects.TryAddStatusEffectDuration(target, StarMarkId, duration);
    }

    private static readonly EntProtoId HallucinationId = "HereticWeeepingHallucinationStatusEffect";

    /// <summary>
    /// Applies Weeping Hallucination status effect to the target.
    /// </summary>
    public void ApplyHallucination(EntityUid target, TimeSpan duration)
    {
        _statusEffects.TryAddStatusEffectDuration(target, HallucinationId, duration);
    }
}
