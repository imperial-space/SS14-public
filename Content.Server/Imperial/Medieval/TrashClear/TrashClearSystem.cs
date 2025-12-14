using Content.Server.TrashClear.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Map.Components;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Server.Storage.Components;
using Content.Shared.Storage;
using Robust.Shared.Random;
using Content.Shared.Storage.Components;

namespace Content.Server.TrashClear;

public sealed class TrashClearSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] protected readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const float DefenceCheckRadius = 6f;
    private const float DefaultReloadTimeSeconds = 1200f;

    private TimeSpan _nextCheckTime;

    private readonly HashSet<EntityUid> _trackedEntities = new(); // Отслеживаемые сущности

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TrashClearComponent, MoveEvent>(OnChangeParent);
        SubscribeLocalEvent<TrashClearComponent, ComponentShutdown>(OnTrashClearShutdown);
        SubscribeLocalEvent<TrashDefendAreaComponent, EntParentChangedMessage>(OnDefenceAreaParentChanged); // Отслеживаем перемещения защитных зон

        _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);
    }

    private void OnTrashClearShutdown(EntityUid uid, TrashClearComponent component, ComponentShutdown args)
    {
        // Убираем из отслеживаемых, если компонент удален
        _trackedEntities.Remove(uid);
    }

    private void OnChangeParent(EntityUid uid, TrashClearComponent comp, ref MoveEvent args)
    {
        if (comp.Safe) return;

        var newParent = args.NewPosition.EntityId;

        if (!args.ParentChanged)
            return;

        var onGround = HasComp<MapGridComponent>(newParent);
        comp.OnTheGround = onGround;
        comp.Enabled = true;
        comp.WillBeDespawnedTime = _timing.CurTime + comp.DespawnTime;

        // Добавляем/удаляем из списка отслеживаемых сущностей.
        if (onGround && comp.Enabled)
        {
            _trackedEntities.Add(uid);
        }
        else
        {
            _trackedEntities.Remove(uid);
        }
    }

    private void OnDefenceAreaParentChanged(EntityUid uid, TrashDefendAreaComponent component, EntParentChangedMessage args)
    {
        // Очищаем кэш близости (если используется) и пересчитываем близость к защитным зонам
        // (это пример, если у вас есть система кэширования защитных зон)
        // RefreshProximityCache();
    }


    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime > _nextCheckTime)
        {
            PerformTrashClear(curTime);
            _nextCheckTime = curTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);
        }
    }

    private void PerformTrashClear(TimeSpan curTime)
    {
        List<EntityUid> toDelete = new();

        foreach (var uid in _trackedEntities) // Используем ToArray, чтобы можно было безопасно удалять из _trackedEntities
        {
            if (!TryComp<TrashClearComponent>(uid, out var comp))
            {
                _trackedEntities.Remove(uid); // Уже нет компонента
                continue;
            }

            if (comp.Safe || !comp.Enabled || !comp.OnTheGround)
            {
                continue;
            }

            if (curTime < comp.WillBeDespawnedTime)
            {
                continue;
            }


            var xform = Transform(uid);
            var coords = xform.Coordinates;

            if (CheckDefenceArea(coords))
            {
                continue;
            }

            // Дополнительные проверки
            bool delete = true;
            if (TryComp<EntityStorageComponent>(uid, out var entstorage) && entstorage.Contents.Count > 0)
                delete = false;
            if (TryComp<StorageComponent>(uid, out var storage) && storage.Container.Count > 0)
                delete = false;

            if (delete)
            {
                toDelete.Add(uid);
                _trackedEntities.Remove(uid);
            }

        }

        // Пакетное удаление (опционально, но может быть эффективнее)
        foreach (var entity in toDelete)
        {
            QueueDel(entity);
        }
    }

    public bool CheckDefenceArea(EntityCoordinates coords)
    {
        foreach (var entity in _lookup.GetEntitiesInRange(coords, DefenceCheckRadius))
        {
            if (TryComp<TrashDefendAreaComponent>(entity, out _))
            {
                return true;
            }
        }
        return false;
    }
}
