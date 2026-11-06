using Content.Server.Disposal.Tube;
using Content.Server.Disposal.Unit;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Disposal.Components;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.NodeContainer;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.ZLevels;

/// <summary>
/// Коммуникации между этажами SS13:
/// кабельный щиток (multi-z cable hub) — сеть питания общая с щитком этажом выше/ниже (сам узел — <see cref="ZCableHubNode"/>);
/// переходник труб (multi-deck adapter) — труба наверху и внизу одна сеть;
/// мусоропровод вверх/вниз — что пришло из сети в такую трубу, выходит из парной на другом этаже.
/// </summary>
public sealed class ZUtilitiesSystem : EntitySystem
{
    [Dependency] private readonly NodeGroupSystem _nodeGroup = default!;
    [Dependency] private readonly DisposableSystem _disposable = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NodeContainerComponent, ZLinkChangedEvent>(OnNodesLinkChanged);
        SubscribeLocalEvent<ZPipeAdapterComponent, ComponentShutdown>(OnAdapterShutdown);
        SubscribeLocalEvent<ZDisposalLiftComponent, GetDisposalsConnectableDirectionsEvent>(OnLiftConnectable);
        SubscribeLocalEvent<ZDisposalLiftComponent, GetDisposalsNextDirectionEvent>(OnLiftNext);
    }

    // ---------- Питание и трубы ----------

    private void OnNodesLinkChanged(Entity<NodeContainerComponent> ent, ref ZLinkChangedEvent args)
    {
        if (TryComp<ZPipeAdapterComponent>(ent, out var adapter))
        {
            RelinkPipes((ent, adapter), ent.Comp);
            return;
        }
        foreach (var node in ent.Comp.Nodes.Values)
        {
            if (node is ZCableHubNode)
                _nodeGroup.QueueReflood(node);
        }
    }

    /// <summary>Узел трубы переходника всегда достижим из узла парного переходника и наоборот.</summary>
    private void RelinkPipes(Entity<ZPipeAdapterComponent> ent, NodeContainerComponent nodes)
    {
        if (GetPipe(ent, nodes) is not { } pipe)
            return;
        foreach (var old in ent.Comp.Linked)
        {
            if (TryComp<ZPipeAdapterComponent>(old, out var oldAdapter) && GetPipe((old, oldAdapter), null) is { } oldPipe)
            {
                pipe.RemoveAlwaysReachable(oldPipe);
                oldPipe.RemoveAlwaysReachable(pipe);
                oldAdapter.Linked.Remove(ent);
            }
        }
        ent.Comp.Linked.Clear();

        if (!TryComp<ZLinkComponent>(ent, out var link))
            return;
        foreach (var other in new[] { link.Up, link.Down })
        {
            if (other is not { } uid || !TryComp<ZPipeAdapterComponent>(uid, out var otherAdapter)
                || GetPipe((uid, otherAdapter), null) is not { } otherPipe)
                continue;
            pipe.AddAlwaysReachable(otherPipe);
            otherPipe.AddAlwaysReachable(pipe);
            ent.Comp.Linked.Add(uid);
            otherAdapter.Linked.Add(ent);
        }
        _nodeGroup.QueueReflood(pipe);
    }

    private void OnAdapterShutdown(Entity<ZPipeAdapterComponent> ent, ref ComponentShutdown args)
    {
        if (GetPipe(ent, null) is not { } pipe)
            return;
        foreach (var old in ent.Comp.Linked)
        {
            if (TryComp<ZPipeAdapterComponent>(old, out var oldAdapter) && GetPipe((old, oldAdapter), null) is { } oldPipe)
            {
                oldPipe.RemoveAlwaysReachable(pipe);
                oldAdapter.Linked.Remove(ent);
            }
        }
        ent.Comp.Linked.Clear();
    }

    private PipeNode? GetPipe(Entity<ZPipeAdapterComponent> ent, NodeContainerComponent? nodes)
    {
        if (nodes == null && !TryComp(ent, out nodes))
            return null;
        return nodes.Nodes.TryGetValue(ent.Comp.Node, out var node) ? node as PipeNode : null;
    }

    // ---------- Мусоропровод ----------

    /// <summary>Одна горизонтальная труба — туда, куда повёрнута, как у входной трубы.</summary>
    private void OnLiftConnectable(Entity<ZDisposalLiftComponent> ent, ref GetDisposalsConnectableDirectionsEvent args)
    {
        args.Connectable = new[] { Transform(ent).LocalRotation.GetDir() };
    }

    /// <summary>
    /// Пришло из сети — переносим в парную трубу на другом этаже (оттуда выйдет в её сеть). Пришло сверху или снизу —
    /// выпускаем в свою сеть. Пары нет — отправляем обратно.
    /// </summary>
    private void OnLiftNext(Entity<ZDisposalLiftComponent> ent, ref GetDisposalsNextDirectionEvent args)
    {
        var port = Transform(ent).LocalRotation.GetDir();
        var link = CompOrNull<ZLinkComponent>(ent);
        var holder = args.Holder;
        if (link != null && holder.PreviousTube is { } prev && (prev == link.Up || prev == link.Down))
        {
            args.Next = port;
            return;
        }

        var target = link == null ? null
            : link.Up != null && link.Down != null ? (ent.Comp.GoesUp ? link.Up : link.Down)
            : link.Up ?? link.Down;
        if (target is not { } targetUid || TerminatingOrDeleted(targetUid) || !TryComp<DisposalTubeComponent>(ent, out var tube))
        {
            args.Next = port;
            return;
        }

        var holderUid = holder.Owner;
        _container.Remove(holderUid, tube.Contents, reparent: false, force: true);
        if (!_disposable.EnterTube(holderUid, targetUid, holder))
        {
            args.Next = Direction.Invalid;
            return;
        }
        // EnterTube в парной трубе уже выставил текущую трубу и направление выхода — повторяем его.
        args.Next = holder.CurrentDirection;
    }
}
