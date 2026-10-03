using Content.Shared.Atmos;
using Content.Shared.Imperial.Hypertorus;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Hypertorus;

/// <summary>Методы для админ-команды «hypertorus»: поставить собранный реактор и заправить его.</summary>
public sealed partial class HypertorusSystem
{
    /// <summary>Ставит собранный реактор 3×3 с центром на клетке и связывает детали, как мультитул по интерфейсу.</summary>
    public EntityUid? SpawnAssembled(EntityCoordinates coordinates)
    {
        if (_xform.GetGrid(coordinates) is not { } gridUid || !TryComp<Robust.Shared.Map.Components.MapGridComponent>(gridUid, out var gridComp))
            return null;

        var grid = new Entity<Robust.Shared.Map.Components.MapGridComponent>(gridUid, gridComp);
        var tile = _map.TileIndicesFor(grid, grid, coordinates);
        var parts = new (EntProtoId Proto, Direction Dir)[]
        {
            ("ImperialHypertorusInterface", Direction.North),
            ("ImperialHypertorusFuelInput", Direction.East),
            ("ImperialHypertorusWasteOutput", Direction.South),
            ("ImperialHypertorusModeratorInput", Direction.West),
        };

        for (var i = 0; i < Cardinals.Length; i++)
        {
            SpawnPart(parts[i].Proto, grid, tile + Cardinals[i].Offset, Cardinals[i].Dir);
        }

        foreach (var (offset, dir) in Diagonals)
        {
            SpawnPart("ImperialHypertorusCorner", grid, tile + offset, dir);
        }

        var core = SpawnPart("ImperialHypertorusCore", grid, tile, Direction.South);
        var coreComp = Comp<HypertorusCoreComponent>(core);
        if (!CheckPartConnectivity((core, coreComp)) || coreComp.Interface is not { } iface)
            return core;

        TryActivate((iface, Comp<HypertorusPartComponent>(iface)), core);
        return core;
    }

    /// <summary>Выбрать рецепт, положить топливо 50/50 и модератор прямо в ядро, включить питание и охлаждение.</summary>
    public bool Fill(EntityUid uid, ProtoId<HypertorusFuelPrototype> fuelId, float fuelMoles, Gas moderatorGas, float moderatorMoles, float temperature = 0)
    {
        if (!TryComp<HypertorusCoreComponent>(uid, out var core) || !_proto.TryIndex(fuelId, out var fuel))
            return false;

        core.SelectedFuel = fuelId;
        foreach (var gas in fuel.Requirements)
        {
            core.InternalFusion.AdjustMoles(gas, fuelMoles / fuel.Requirements.Count);
        }

        if (moderatorMoles > 0)
            core.ModeratorInternal.AdjustMoles(moderatorGas, moderatorMoles);

        if (temperature > core.FusionTemp)
            core.FusionTemp = temperature;

        core.StartPower = true;
        core.StartCooling = true;
        core.HeatingConductor = 500;
        return true;
    }

    public string Status(EntityUid uid)
    {
        if (!TryComp<HypertorusCoreComponent>(uid, out var core))
            return "not a hypertorus core";

        return $"{ToPrettyString(uid)}: active={core.Active} power={core.StartPower} level={core.PowerLevel} " +
               $"fusionT={core.FusionTemp:0.##}K moderatorT={core.ModeratorTemp:0.##}K powered={TryComp<Content.Server.Power.Components.ApcPowerReceiverComponent>(uid, out var r) && r.Powered} fusionMol={core.InternalFusion.TotalMoles:0.##} " +
               $"moderatorMol={core.ModeratorInternal.TotalMoles:0.##} heat={core.HeatOutput:0.###} " +
               $"energy={core.Energy:0.###e+0} instability={core.Instability:0.###} integrity={GetIntegrityPercent(core)}% " +
               $"iron={core.IronContent:0.###} countdown={core.FinalCountdown}";
    }

    public string PartsInfo(EntityUid uid)
    {
        if (!TryComp<HypertorusCoreComponent>(uid, out var core))
            return string.Empty;

        var lines = new List<string>();
        foreach (var part in LinkedParts(core))
        {
            var node = _nodeContainer.TryGetNode(part, HypertorusCoreComponent.PipeNode, out Content.Server.NodeContainer.Nodes.PipeNode? p) ? p.CurrentPipeDirection.ToString() : "-";
            lines.Add($"{MetaData(part).EntityPrototype?.ID} rot={Transform(part).LocalRotation.Degrees:0} dir={GetDir(part)} pipe={node}");
        }
        return string.Join("; ", lines);
    }
}
