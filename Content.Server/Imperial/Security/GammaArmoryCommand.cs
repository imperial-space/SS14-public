using System.Numerics;
using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Atmos.Components;
using Content.Shared.Gravity;
using Content.Shared.Materials;
using Content.Shared.Maps;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>Админ-команда: прислать или отозвать гамма-оружейку.</summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class GammaArmoryCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "gammaarmory";
    public string Description => Loc.GetString("imperial-security-gamma-armory-command-1");
    public string Help => Command;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine(_entManager.System<GammaArmorySystem>().Toggle());
    }
}
