using System.Collections.Generic;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Additions;

public abstract class MedievalSharedTimedDespawnSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;

    private readonly HashSet<EntityUid> _queuedDespawnEntities = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;
        SubscribeLocalEvent<MedievalTimedDespawnComponent, ComponentStartup>(Start);
    }
    public void Start(EntityUid uid, MedievalTimedDespawnComponent component, ComponentStartup args)
    {
        component.OriginalLifeTime = component.Lifetime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // AAAAAAAAAAAAAAAAAAAAAAAAAAA
        // Client both needs to predict this, but also can't properly handle prediction resetting.
        if (!_timing.IsFirstTimePredicted)
            return;

        _queuedDespawnEntities.Clear();

        var query = EntityQueryEnumerator<MedievalTimedDespawnComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            var check = true;
            if (HasComp<ContainerManagerComponent>(uid))
            {
                foreach (var container in _container.GetAllContainers(uid))
                {
                    foreach (var contained in container.ContainedEntities)
                    {
                        if (_players.TryGetSessionByEntity(contained, out _))
                        {
                            check = false;
                            break;
                        }
                    }
                    if (!check)
                        break;
                }
            }
            if (check)
            {
                var queryxform = Transform(uid).ChildEnumerator;
                while (queryxform.MoveNext(out var contained))
                {
                    if (_players.TryGetSessionByEntity(contained, out _))
                    {
                        check = false;
                        break;
                    }
                    if (!check)
                        break;
                }
            }
            if (check)
            {
                if (_players.TryGetSessionByEntity(uid, out _))
                {
                    check = false;
                }
            }
            if (!check)
            {
                comp.Lifetime = comp.OriginalLifeTime;
                continue;
            }
            comp.Lifetime -= frameTime;
            if (!CanDelete(uid))
                continue;

            if (comp.Lifetime <= 0)
            {
                _queuedDespawnEntities.Add(uid);
            }
        }

        foreach (var queued in _queuedDespawnEntities)
        {
            var ev = new TimedDespawnEvent();
            RaiseLocalEvent(queued, ref ev);
            QueueDel(queued);
        }
    }

    protected abstract bool CanDelete(EntityUid uid);
}
