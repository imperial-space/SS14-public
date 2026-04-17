using Content.Shared.Antag;
using Content.Shared.Imperial.Cult.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Shared.Imperial.Cult;

/// <summary>
/// Handles session-specific state for <see cref="CultistComponent"/> so that
/// only fellow cultists (and admins with <see cref="ShowAntagIconsComponent"/>)
/// receive the component via game state, enabling the faction HUD icons to work
/// the same way as the Revolution system does.
/// </summary>
public sealed class SharedCultSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultistComponent, ComponentGetStateAttemptEvent>(OnCultistGetStateAttempt);
    }

    private void OnCultistGetStateAttempt(EntityUid uid, CultistComponent comp, ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = !CanGetState(args.Player);
    }

    private bool CanGetState(ICommonSession? player)
    {
        // Replays — always allow.
        if (player?.AttachedEntity is not { } uid)
            return true;

        if (HasComp<CultistComponent>(uid))
            return true;

        return HasComp<ShowAntagIconsComponent>(uid);
    }

}
