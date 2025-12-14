using System.Diagnostics;
using Content.Server.Administration.Managers;
using Content.Server.Afk;
using Content.Shared.CCVar;
using Content.Shared.Imperial.Medieval.CCVar;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Afk;

public interface IMedievalAfkManager
{
    AfkState GetAfkState(ICommonSession player);
    void PlayerDidAction(ICommonSession player);

    void Initialize();
}


[UsedImplicitly]
public sealed class MedievalAfkManager : IMedievalAfkManager
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IConsoleHost _consoleHost = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    private readonly Dictionary<ICommonSession, TimeSpan> _lastActionTimes = new();

    public void Initialize()
    {
        _playerManager.PlayerStatusChanged += PlayerStatusChanged;
        _consoleHost.AnyCommandExecuted += ConsoleHostOnAnyCommandExecuted;
    }
    public void PlayerDidAction(ICommonSession player)
    {
        if (player.Status == SessionStatus.Disconnected)
            return;
        _lastActionTimes[player] = _gameTiming.RealTime;
    }
    public AfkState GetAfkState(ICommonSession player)
    {
        if (!_lastActionTimes.TryGetValue(player, out var time))
            return AfkState.Active;

        var elapsed = _gameTiming.RealTime - time;

        var afkTime = TimeSpan.FromSeconds(_cfg.GetCVar(MedievalCCVars.AfkTime));
        var kickTime = TimeSpan.FromSeconds(_cfg.GetCVar(MedievalCCVars.AfkKickTime));

        if (elapsed > afkTime + kickTime)
            return AfkState.Timeout;
        if (elapsed > afkTime)
            return AfkState.Afk;

        return AfkState.Active;
    }


    private void PlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
        {
            _lastActionTimes.Remove(e.Session);
            return;
        }
        PlayerDidAction(e.Session);
    }
    private void ConsoleHostOnAnyCommandExecuted(IConsoleShell shell, string commandname, string argstr, string[] args)
    {
        if (shell.Player is { } player)
            PlayerDidAction(player);
    }
}


public enum AfkState
{
    Active,
    Afk,
    Timeout,
}
