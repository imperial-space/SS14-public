using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Movement.Events;
using Content.Shared.Standing;
using Robust.Shared.GameStates;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.IdentityManagement;
using Robust.Shared.Timing;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;

namespace Content.Shared.Imperial.Aquila.FeignDeath;

[RegisterComponent, NetworkedComponent]
public sealed partial class FeignDeathComponent : Component
{
    public TimeSpan ActivatedAt;

    [DataField]
    public TimeSpan MoveIgnoreDuration = TimeSpan.FromSeconds(0.5);
}

public sealed class FeignDeathSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StandingStateComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<FeignDeathComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FeignDeathComponent, MoveInputEvent>(OnMoveInput);
    }

    private void OnEmote(EntityUid uid, StandingStateComponent _, ref EmoteEvent args)
    {
        if (args.Emote.ID != "DefaultDeathgasp")
            return;

        if (!HasComp<FeignDeathComponent>(uid))
        {
            var comp = AddComp<FeignDeathComponent>(uid);
            comp.ActivatedAt = _timing.CurTime;
        }
    }

    private void OnMoveInput(EntityUid uid, FeignDeathComponent comp, ref MoveInputEvent args)
    {
        if (_timing.CurTime < comp.ActivatedAt + comp.MoveIgnoreDuration)
            return;
        if (!TryComp<InputMoverComponent>(uid, out var mover))
            return;

        var directionalButtons = mover.HeldMoveButtons & ~MoveButtons.Walk;
        if (directionalButtons == MoveButtons.None)
            return;

        RemCompDeferred<FeignDeathComponent>(uid);
    }
    private void OnExamined(EntityUid uid, FeignDeathComponent comp, ref ExaminedEvent args)
    {
        var description = "perishable-1";
        args.PushMarkup(Loc.GetString($"[color=red]{Loc.GetString("comp-mind-examined-dead", ("ent", comp.Owner))}[/color]"));
        args.PushMarkup(Loc.GetString(description, ("target", Identity.Entity(uid, EntityManager))));
    }
}
