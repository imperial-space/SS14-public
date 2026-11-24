using Content.Client.Gameplay;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.SyndieBattle.Events;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.SyndieBattle.UI;

public sealed class SyndieBattleRespawnUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEntityNetworkManager _entityNetworkManager = default!;

    private SyndieBattleRespawnControl? _control;
    private TimeSpan _readyAt;
    private bool _visible;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SyndieBattleModeStatusEvent>(OnModeStatus);
        SubscribeNetworkEvent<SyndieBattleRespawnUiStateEvent>(OnRespawnState);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_control is { Visible: true } control)
            control.UpdateCooldown(CooldownRemaining());
    }

    public void OnStateEntered(GameplayState state)
    {
        if (_control != null)
            return;

        _control = new SyndieBattleRespawnControl();
        _control.OnRespawn += OnRespawn;
        UIManager.RootControl.AddChild(_control);
        UpdateControl();
    }

    public void OnStateExited(GameplayState state)
    {
        if (_control is { } control)
        {
            control.OnRespawn -= OnRespawn;
            control.Orphan();
            _control = null;
        }

        _visible = false;
        _readyAt = TimeSpan.Zero;
    }

    private void OnModeStatus(SyndieBattleModeStatusEvent message, EntitySessionEventArgs args)
    {
        if (!message.Active)
        {
            _visible = false;
            UpdateControl();
        }
    }

    private void OnRespawnState(SyndieBattleRespawnUiStateEvent message, EntitySessionEventArgs args)
    {
        _visible = message.Visible;
        _readyAt = _timing.CurTime + message.CooldownRemaining;
        UpdateControl();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent message, EntitySessionEventArgs args)
    {
        _visible = false;
        _readyAt = TimeSpan.Zero;
        UpdateControl();
    }

    private TimeSpan CooldownRemaining()
    {
        var remaining = _readyAt - _timing.CurTime;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private void UpdateControl()
    {
        if (_control is not { } control)
            return;

        control.Visible = _visible;
        control.UpdateCooldown(CooldownRemaining());
    }

    private void OnRespawn()
    {
        _entityNetworkManager.SendSystemNetworkMessage(new SyndieBattleRespawnRequestEvent());
    }
}
