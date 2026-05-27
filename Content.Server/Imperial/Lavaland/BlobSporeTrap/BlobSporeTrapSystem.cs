using Content.Shared.Gibbing;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Lavaland.BlobSporeTrap;

/// <summary>
/// Обрабатывает ловушку споры блоба.
/// Каждые <see cref="BlobSporeTrapComponent.CheckRate"/> секунд сканирует ближайших мобов;
/// при обнаружении — гибает первого найденного и удаляет спору.
/// </summary>
public sealed class BlobSporeTrapSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlobSporeTrapComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.AccumulatedTime += frameTime;
            if (comp.AccumulatedTime < comp.CheckRate)
                continue;
            comp.AccumulatedTime = 0f;

            CheckAndTrigger(uid, comp);
        }
    }

    private void CheckAndTrigger(EntityUid uid, BlobSporeTrapComponent comp)
    {
        var coords = Transform(uid).Coordinates;
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(coords, comp.CheckRadius, nearby);

        foreach (var entity in nearby)
        {
            if (entity == uid)
                continue;

            // Реагируем только на живых мобов
            if (!HasComp<MobStateComponent>(entity))
                continue;

            // Гибаем сущность и самоуничтожаемся
            _gibbing.Gib(entity);
            QueueDel(uid);
            return;
        }
    }
}
