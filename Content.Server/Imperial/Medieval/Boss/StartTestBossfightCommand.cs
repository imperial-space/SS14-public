using System.Linq;
using Content.Server.Administration.Systems;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Administration;
using Content.Shared.Humanoid;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class StartTestBossfightCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly AdminSystem _admin = default!;

    public override string Command => "testboss";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var players = _entMan.AllEntities<HumanoidAppearanceComponent>().Select(x => x.Owner).ToList();
        var boss = _entMan.AllEntities<BossComponent>().First();

        _entMan.System<BossSystem>().StartBossfight(players, boss);
    }
}
