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
    public string Description => "Загружает трудовой лагерь, если его ещё нет, и показывает маяк гулага.";
    public string Help => Command;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_entManager.System<LaborCampMapSystem>().EnsureCamp())
        {
            shell.WriteLine("Не удалось загрузить трудовой лагерь.");
            return;
        }

        var query = _entManager.EntityQueryEnumerator<GulagBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            shell.WriteLine($"Маяк гулага: {_entManager.ToPrettyString(uid)}, карта {xform.MapID}.");
        }
    }
}
