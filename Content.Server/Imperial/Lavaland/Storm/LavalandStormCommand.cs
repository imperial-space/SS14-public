using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Imperial.Lavaland.Storm;

[AdminCommand(AdminFlags.Fun)]
public sealed class LavalandStormCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override string Command => "lavalandstorm";
    public override string Description => "Force-starts an ash storm on the Lavaland map.";
    public override string Help => "lavalandstorm";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var stormSystem = _entityManager.System<LavalandStormSystem>();
        var query = _entityManager.EntityQueryEnumerator<LavalandMapComponent>();

        var found = false;
        while (query.MoveNext(out var mapUid, out var comp))
        {
            found = true;
            if (comp.StormState == LavalandStormState.Active)
            {
                shell.WriteLine("Буря на Лаваленде уже бушует.");
                return;
            }
            stormSystem.ForceStartStorm(mapUid, comp);
            shell.WriteLine("Буря на Лаваленде запущена.");
            return;
        }

        if (!found)
            shell.WriteError("Лаваленд не найден.");
    }
}
