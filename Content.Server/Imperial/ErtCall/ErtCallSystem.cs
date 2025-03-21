using Robust.Shared.Map;
using Content.Shared.Imperial.ErtCall;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Utility;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.ErtCall;
public sealed class CallErtSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;

    public bool SpawnErt(ErtCallPresetPrototype preset)
    {
        var shuttleMapUid = _mapSystem.CreateMap();
        var mapId = Comp<MapComponent>(shuttleMapUid).MapId;

        var options = new DeserializationOptions()
        {
            InitializeMaps = true
        };

        return _map.TryLoadGrid(mapId, new ResPath(preset.Path), out _, options);
    }
}

