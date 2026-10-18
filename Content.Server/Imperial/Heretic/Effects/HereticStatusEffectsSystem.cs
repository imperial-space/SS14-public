using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Items;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Imperial.Heretic.Paths.Moon.Parade;
using Content.Server.Imperial.Heretic.Paths.Void;
using Content.Server.Popups;
using Content.Server.Temperature.Systems;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic.Effects;

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
        SubscribeLocalEvent<Content.Server.Chat.Systems.ExpandICChatRecipientsEvent>(OnExpandRecipients);
        SubscribeLocalEvent<HereticInsanityStatusEffectComponent, StatusEffectAppliedEvent>(OnInsanityApplied);
        SubscribeLocalEvent<VoidChillStatusEffectComponent, StatusEffectAppliedEvent>(OnVoidChillApplied);
        SubscribeLocalEvent<VoidChillStatusEffectComponent, StatusEffectRemovedEvent>(OnVoidChillRemoved);
    }

    /// <summary>sound_damage(): оглохшая цель не слышит чужую речь.</summary>
    private void OnExpandRecipients(Content.Server.Chat.Systems.ExpandICChatRecipientsEvent args)
    {
        var deaf = new List<Robust.Shared.Player.ICommonSession>();
        foreach (var (session, data) in args.Recipients)
        {
            if (data.Observer || session.AttachedEntity is not { } listener || listener == args.Source)
                continue;

            if (HasComp<HereticDeafComponent>(listener))
                deaf.Add(session);
        }

        foreach (var session in deaf)
        {
            args.Recipients.Remove(session);
        }
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

        var deafQuery = EntityQueryEnumerator<HereticDeafComponent>();
        while (deafQuery.MoveNext(out var deafUid, out var deaf))
        {
            if (_timing.CurTime >= deaf.ExpiresAt)
                RemCompDeferred<HereticDeafComponent>(deafUid);
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

    private static readonly EntProtoId MoonDelusionId = "HereticMoonDelusionStatusEffect";

    /// <summary>delusion/preset/moon: носитель видит всех вокруг лунными массами.</summary>
    public void ApplyMoonDelusion(EntityUid target, TimeSpan duration)
    {
        _statusEffects.TryAddStatusEffectDuration(target, MoonDelusionId, duration);
    }

    public void RemoveMoonDelusion(EntityUid target)
    {
        _statusEffects.TryRemoveStatusEffect(target, MoonDelusionId);
    }

    /// <summary>sound_damage(0, duration): цель глохнет на указанное время.</summary>
    public void ApplyDeafness(EntityUid target, TimeSpan duration)
    {
        var comp = EnsureComp<HereticDeafComponent>(target);
        var until = _timing.CurTime + duration;
        if (until > comp.ExpiresAt)
            comp.ExpiresAt = until;
    }
}
