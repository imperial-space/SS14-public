using System.Linq;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Station;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Reflection;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>/datum/round_event/ghost_role/bitrunning_glitch: админ-ивент «Глюк битраннинга».</summary>
public sealed class BitrunningGlitchRuleSystem : GameRuleSystem<BitrunningGlitchRuleComponent>
{
    [Dependency] private readonly QuantumServerSystem _servers = default!;

    protected override void Started(EntityUid uid, BitrunningGlitchRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var servers = new List<Entity<QuantumServerComponent>>();
        var query = EntityQueryEnumerator<QuantumServerComponent>();
        while (query.MoveNext(out var server, out var comp))
        {
            if (comp.Domain != null && comp.Ready)
                servers.Add((server, comp));
        }

        RobustRandom.Shuffle(servers);
        foreach (var server in servers)
        {
            if (_servers.SetupGlitch(server, component.ForcedSpawner))
                break;
        }

        GameTicker.EndGameRule(uid, gameRule);
    }
}
