using System.IO;
using Content.Shared.Maps;
using Robust.Shared.Utility;

namespace Content.Server.Maps;

public sealed partial class GameMapManager
{
    // Imperial Weekly Mode Start
    public void SelectMapPath(string baseMapPrototype, ResPath mapPath)
    {
        if (!TryLookupMap(baseMapPrototype, out var map))
            throw new ArgumentException($"The map \"{baseMapPrototype}\" is invalid!");

        if (!_resMan.ContentFileExists(mapPath))
            throw new FileNotFoundException($"The map file \"{mapPath}\" does not exist in content resources.");

        _selectedMap = map.Persistence(mapPath);
        _log.Info($"Using map prototype {baseMapPrototype} with weekly map path {mapPath}");
    }
    // Imperial Weekly Mode End

    // Imperial Weekly Mode Start
    public void SelectPersistentMap(string baseMapPrototype, ResPath mapPath)
    {
        if (!TryLookupMap(baseMapPrototype, out var map))
            throw new ArgumentException($"The map \"{baseMapPrototype}\" is invalid!");

        _selectedMap = ResolvePersistenceVariant(map, mapPath);
    }

    private GameMapPrototype ResolvePersistenceVariant(GameMapPrototype map, ResPath mapPath)
    {
        if (_resMan.UserData.Exists(mapPath))
        {
            _log.Info($"Using persistence map from {mapPath}");
            return map.Persistence(mapPath);
        }

        _log.Warning($"Using persistence start map {map.ID} as {mapPath} doesn't exist");
        return map;
    }

}
