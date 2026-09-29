using Content.Server.Doors.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Ash;
using Content.Shared.Imperial.Heretic.Paths.Blade;
using Content.Shared.Imperial.Heretic.Paths.Cosmos;
using Content.Shared.Imperial.Heretic.Paths.Flesh;
using Content.Shared.Imperial.Heretic.Paths.Lock;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Rust;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Время жизни меток путей: метка спадает, если еретик долго не обновлял её Хваткой Мансуса.
/// Также убирает последствия метки, когда её снимают любым способом.
/// </summary>
public sealed class HereticMarkSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DoorSystem _door = default!;
    [Dependency] private readonly SharedAccessSystem _access = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeMark<AshMarkComponent>();
        SubscribeMark<HereticBladeMarkComponent>();
        SubscribeMark<FleshMarkComponent>();
        SubscribeMark<LockMarkComponent>();
        SubscribeMark<MoonMarkComponent>();
        SubscribeMark<RustMarkComponent>();
        SubscribeMark<VoidMarkComponent>();
        SubscribeMark<CosmosMarkComponent>();

        SubscribeLocalEvent<HereticBladeMarkComponent, ComponentShutdown>(OnBladeMarkShutdown);
        SubscribeLocalEvent<LockMarkComponent, ComponentShutdown>(OnLockMarkShutdown);
        SubscribeLocalEvent<CosmosMarkComponent, ComponentShutdown>(OnCosmosMarkShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ExpireMarks<AshMarkComponent>();
        ExpireMarks<HereticBladeMarkComponent>();
        ExpireMarks<FleshMarkComponent>();
        ExpireMarks<LockMarkComponent>();
        ExpireMarks<MoonMarkComponent>();
        ExpireMarks<RustMarkComponent>();
        ExpireMarks<VoidMarkComponent>();
        ExpireMarks<CosmosMarkComponent>();
    }

    /// <summary>
    /// Продлевает метку на её полное время жизни. Вызывать при повторном наложении метки.
    /// </summary>
    public void Refresh(IHereticMarkComponent mark)
    {
        mark.ExpireTime = _timing.CurTime + mark.Lifetime;
    }

    private void SubscribeMark<T>() where T : IComponent, IHereticMarkComponent
    {
        SubscribeLocalEvent<T, ComponentStartup>((_, mark, _) => Refresh(mark));
    }

    private void ExpireMarks<T>() where T : IComponent, IHereticMarkComponent
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out var mark))
        {
            if (now >= mark.ExpireTime)
                RemCompDeferred<T>(uid);
        }
    }

    private void OnBladeMarkShutdown(Entity<HereticBladeMarkComponent> ent, ref ComponentShutdown args)
    {
        foreach (var door in ent.Comp.LockedDoors)
        {
            if (TryComp<DoorBoltComponent>(door, out var bolt))
                _door.TrySetBoltDown((door, bolt), false);
        }

        ent.Comp.LockedDoors.Clear();
    }

    private void OnLockMarkShutdown(Entity<LockMarkComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.IdCard is { } card && TryComp<AccessComponent>(card, out var access))
            _access.SetAccessEnabled(card, true, access);
    }

    private void OnCosmosMarkShutdown(Entity<CosmosMarkComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.AnchorEntity is { } anchor && !TerminatingOrDeleted(anchor))
            QueueDel(anchor);
    }
}
