using Content.Server.Power.Nodes;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.ZLevels;

/// <summary>
/// Узел межэтажного кабельного щитка (multi-z cable hub): цепляется к кабелю в своей клетке, как обычное
/// устройство, и к узлу той же сети у щитка этажом выше и ниже (<see cref="ZLinkComponent"/>).
/// </summary>
[DataDefinition]
public sealed partial class ZCableHubNode : CableDeviceNode
{
    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        foreach (var node in base.GetReachableNodes(xform, nodeQuery, xformQuery, grid, entMan))
            yield return node;

        if (!entMan.TryGetComponent<ZLinkComponent>(Owner, out var link))
            yield break;
        foreach (var other in new[] { link.Up, link.Down })
        {
            if (other is not { } uid || !nodeQuery.TryGetComponent(uid, out var container))
                continue;
            foreach (var node in container.Nodes.Values)
            {
                if (node is ZCableHubNode && node.NodeGroupID == NodeGroupID && !node.Deleting)
                    yield return node;
            }
        }
    }
}
