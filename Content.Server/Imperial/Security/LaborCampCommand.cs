using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Security;

/// <summary>Заранее загрузить трудовой лагерь (если Лаваленд не запущен) и показать, где маяк.</summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class LaborCampCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "laborcamp";
    public string Description => Loc.GetString("imperial-security-labor-camp-command-1");
    public string Help => Command;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_entManager.System<LaborCampMapSystem>().EnsureCamp())
        {
            shell.WriteLine(Loc.GetString("imperial-security-labor-camp-command-2"));
            return;
        }

        var query = _entManager.EntityQueryEnumerator<GulagBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            shell.WriteLine(Loc.GetString("imperial-security-labor-camp-command-3", ("arg0", _entManager.ToPrettyString(uid)), ("arg1", xform.MapID)));
        }
    }
}
