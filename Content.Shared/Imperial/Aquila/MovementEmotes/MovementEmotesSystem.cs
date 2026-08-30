using Content.Shared.Imperial.Aquila.Backflip;
using Content.Shared.Imperial.Aquila.Jump;
using Content.Shared.Chat;
using Content.Shared.Movement.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Standing;
using Robust.Shared.Timing;
using Content.Shared.Buckle.Components;

namespace Content.Shared.Imperial.Aquila.MovementEmotes;

public sealed class MovementEmotesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MovementSpeedModifierComponent, EmoteEvent>(OnEmote);
    }
    private void OnEmote(EntityUid uid, MovementSpeedModifierComponent _, ref EmoteEvent args)
    {
        if (args.Emote.ID != "Jump" && args.Emote.ID != "Backflip")
            return;

        if (HasComp<JumpEmoteComponent>(uid) || HasComp<BackflipEmoteComponent>(uid))
            return;

        if (TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled)
            return;

        if (TryComp<StandingStateComponent>(uid, out var standing) && !standing.Standing)
            return;

        switch (args.Emote.ID)
        {
            case "Jump":
                StartJump(uid);
            break;

            case "Backflip":
                StartBackflip(uid);
            break;
        }

    }

    private void StartJump(EntityUid uid)
    {
        var comp = EnsureComp<JumpEmoteComponent>(uid);
        comp.PhaseEndTime = _timing.CurTime + comp.PhaseDuration;

        if (TryComp<StaminaComponent>(uid, out var stamina))
            _stamina.TakeStaminaDamage(uid, stamina.CritThreshold * comp.StaminaCostFraction, stamina);

        RaiseLocalEvent(uid, new StartJumpPhaseEvent());
    }

    private void StartBackflip(EntityUid uid)
    {
        var comp = EnsureComp<BackflipEmoteComponent>(uid);
        comp.PhaseEndTime = _timing.CurTime + comp.PhaseDuration;

        if (TryComp<StaminaComponent>(uid, out var stamina))
            _stamina.TakeStaminaDamage(uid, stamina.CritThreshold * comp.StaminaCostFraction, stamina);

        RaiseLocalEvent(uid, new StartBackflipPhaseEvent());
    }
}
