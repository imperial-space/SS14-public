using Content.Shared.Maps;
// Imperial Weekly Mode Start
using Robust.Shared.Utility;
// Imperial Weekly Mode End

namespace Content.Server.Maps;

/// <summary>
/// Manages which station map will be used for the next round.
/// </summary>
public interface IGameMapManager
{
    void Initialize();

    /// <summary>
    /// Returns all maps eligible to be played right now.
    /// </summary>
    /// <returns>enumerator of map prototypes</returns>
    IEnumerable<GameMapPrototype> CurrentlyEligibleMaps();

    /// <summary>
    /// Returns all maps that can be voted for.
    /// </summary>
    /// <returns>enumerator of map prototypes</returns>
    IEnumerable<GameMapPrototype> AllVotableMaps();

    /// <summary>
    /// Returns all maps.
    /// </summary>
    /// <returns>enumerator of map prototypes</returns>
    IEnumerable<GameMapPrototype> AllMaps();

    /// <summary>
    /// Gets the currently selected map
    /// </summary>
    /// <returns>selected map</returns>
    GameMapPrototype? GetSelectedMap();

    /// <summary>
    /// Clears the selected map, if any
    /// </summary>
    void ClearSelectedMap();

    /// <summary>
    /// Attempts to select the given map, checking eligibility criteria
    /// </summary>
    /// <param name="gameMap">map prototype</param>
    /// <returns>success or failure</returns>
    bool TrySelectMapIfEligible(string gameMap);

    /// <summary>
    /// Select the given map regardless of eligibility
    /// </summary>
    /// <param name="gameMap">map prototype</param>
    /// <returns>success or failure</returns>
    void SelectMap(string gameMap);

    /// <summary>
    // Imperial Weekly Mode Start
    /// Selects a map prototype while replacing its content map path for this selection only.
    /// </summary>
    void SelectMapPath(string baseMapPrototype, ResPath mapPath);

    /// <summary>
    /// Selects a map prototype, replacing its map path with a saved user-data map if it exists.
    /// Falls back to the base map prototype if the saved map path is missing.
    /// </summary>
    void SelectPersistentMap(string baseMapPrototype, ResPath mapPath);

    /// <summary>
    // Imperial Weekly Mode End
    /// Selects a random map eligible map
    /// </summary>
    void SelectMapRandom();

    /// <summary>
    /// Selects the map at the front of the rotation queue
    /// </summary>
    /// <returns>selected map</returns>
    void SelectMapFromRotationQueue(bool markAsPlayed = false);

    /// <summary>
    /// Selects the map by following rules set in the config
    /// </summary>
    public void SelectMapByConfigRules();

    /// <summary>
    /// Checks if the given map exists
    /// </summary>
    /// <param name="gameMap">name of the map</param>
    /// <returns>existence</returns>
    bool CheckMapExists(string gameMap);
}
