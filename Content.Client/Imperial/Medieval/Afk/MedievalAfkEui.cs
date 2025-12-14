using Content.Client.Eui;
using Content.Shared.Eui;
using Content.Shared.Imperial.Medieval.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client.Imperial.Medieval.Afk;

public sealed class MedievalAfkEui : BaseEui
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private AfkWarningWindow _window;


    public MedievalAfkEui()
    {
        IoCManager.InjectDependencies(this);

        _window = new AfkWarningWindow();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
    }

    public override void Opened()
    {
        base.Opened();

        var afk = TimeSpan.FromSeconds(_cfg.GetCVar(MedievalCCVars.AfkTime));
        var kick = TimeSpan.FromSeconds(_cfg.GetCVar(MedievalCCVars.AfkKickTime));

        var afkText = $"{Math.Floor(afk.TotalMinutes)} мин";
        var kickText = $"{Math.Floor(kick.TotalSeconds)} сек";

        _window.WarningText.Text = Loc.GetString("afk-warning-text", ("afk", afkText), ("kick", kickText));
        _window.Remaining = (float)kick.TotalSeconds;

        _window.AfkText = afkText;
        _window.KickText = kickText;

        _window?.OpenCentered();
    }


    public override void Closed()
    {
        base.Closed();

        _window?.Dispose();
    }
}
