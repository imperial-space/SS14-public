using Content.Shared.Imperial.ZLevels;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.ZLevels;

/// <summary>Связь коммуникаций между этажами поменялась (кто-то выше или ниже появился или пропал).</summary>
[ByRefEvent]
public readonly record struct ZLinkChangedEvent;

/// <summary>
/// Связывает межэтажные коммуникации (<see cref="ZLinkComponent"/>) так же, как трапы: с сущностью того же вида
/// ровно этажом выше и ниже в той же клетке. При каждом изменении связи на обеих сторонах поднимается
/// <see cref="ZLinkChangedEvent"/> — питание, трубы и мусоропровод пересобирают свои сети.
/// </summary>
public sealed class ZLinkSystem : EntitySystem
{
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZLinkComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ZLinkComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZLinkComponent, AnchorStateChangedEvent>(OnAnchor);
        SubscribeLocalEvent<ZLevelsRelinkedEvent>(OnRelinked);
    }

    private void OnMapInit(Entity<ZLinkComponent> ent, ref MapInitEvent args) => Link(ent);

    private void OnShutdown(Entity<ZLinkComponent> ent, ref ComponentShutdown args) => Disconnect(ent);

    private void OnAnchor(Entity<ZLinkComponent> ent, ref AnchorStateChangedEvent args)
    {
        Disconnect(ent);
        if (args.Anchored)
            Link(ent);
    }

    private void OnRelinked(ZLevelsRelinkedEvent args)
    {
        var maps = new HashSet<MapId>();
        foreach (var level in _z.GetStack(args.Stack))
            maps.Add(Comp<MapComponent>(level).MapId);
        var query = EntityQueryEnumerator<ZLinkComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var link, out var xform))
        {
            if (maps.Contains(xform.MapID))
                Link((uid, link));
        }
    }

    public void Link(Entity<ZLinkComponent> ent)
    {
        if (TerminatingOrDeleted(ent) || !Transform(ent).Anchored)
            return;
        var here = _z.SnapToTile(_xform.GetMapCoordinates(ent));
        if (ent.Comp.Down == null && Find(here, false, ent.Comp.Kind) is { } down)
            Pair(ent, down);
        if (ent.Comp.Up == null && Find(here, true, ent.Comp.Kind) is { } up)
            Pair(up, ent);
    }

    private Entity<ZLinkComponent>? Find(MapCoordinates here, bool up, string kind)
    {
        if (_z.GetStep(here, up) is not { } other)
            return null;
        foreach (var e in _z.GetAnchored(other))
        {
            if (TryComp<ZLinkComponent>(e, out var link) && link.Kind == kind)
                return (e, link);
        }
        return null;
    }

    private void Pair(Entity<ZLinkComponent> upper, Entity<ZLinkComponent> lower)
    {
        upper.Comp.Down = lower;
        lower.Comp.Up = upper;
        Changed(upper);
        Changed(lower);
    }

    private void Disconnect(Entity<ZLinkComponent> ent)
    {
        var up = ent.Comp.Up;
        var down = ent.Comp.Down;
        ent.Comp.Up = null;
        ent.Comp.Down = null;
        if (down is { } d && TryComp<ZLinkComponent>(d, out var downLink) && downLink.Up == ent.Owner)
        {
            downLink.Up = null;
            Changed(d);
        }
        if (up is { } u && TryComp<ZLinkComponent>(u, out var upLink) && upLink.Down == ent.Owner)
        {
            upLink.Down = null;
            Changed(u);
        }
        if (!TerminatingOrDeleted(ent))
            Changed(ent);
    }

    private void Changed(EntityUid uid)
    {
        var ev = new ZLinkChangedEvent();
        RaiseLocalEvent(uid, ref ev);
    }
}
