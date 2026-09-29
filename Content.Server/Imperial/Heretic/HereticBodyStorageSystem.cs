using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Прячет тело, пока его владелец в другой форме (червь, сфера пепла, космическая фаза, шар Девы).
/// </summary>
/// <remarks>
/// Тело переносится на отдельную карту на паузе. В nullspace его нельзя оставлять: там оно не на паузе,
/// дыхание видит вакуум, давление бьёт баротравмой, тратятся голод и жажда — тело получает урон или умирает.
/// Возвращать тело достаточно обычным SetCoordinates: при переходе на живую карту пауза снимается сама.
/// </remarks>
public sealed class HereticBodyStorageSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private EntityUid? _storageMap;

    public void StoreBody(EntityUid body)
    {
        _xform.SetCoordinates(body, new EntityCoordinates(EnsureStorageMap(), Vector2.Zero));
    }

    private EntityUid EnsureStorageMap()
    {
        if (_storageMap is { } existing && Exists(existing))
            return existing;

        var map = _map.CreateMap();
        _metaData.SetEntityName(map, "Heretic body storage");
        _map.SetPaused(map, true);
        _storageMap = map;
        return map;
    }
}
