using System;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Shared.Imperial.Medieval.Ships.Hull;

/// <summary>
/// Resolves the shared ship hull tile progression for waves, repairs and flooding.
/// </summary>
public sealed class SharedShipHullSystem : EntitySystem
{
    [Dependency] private readonly ITileDefinitionManager _tileDefinitions = default!;

    private const string IntactHullTile = "FloorWood";

    private static readonly string[] DamagedHullTiles =
    {
        "woodbroken1",
        "woodbroken2",
        "woodbroken3",
    };

    private readonly ushort[] _damagedHullTileIds = new ushort[DamagedHullTiles.Length];
    private ushort _intactHullTileId;
    private bool _initialized;

    public ushort IntactHullTileId
    {
        get
        {
            EnsureInitialized();
            return _intactHullTileId;
        }
    }

    public int MaxDamageStage => _damagedHullTileIds.Length;

    public bool TryGetDamageStage(int tileTypeId, out int stage)
    {
        EnsureInitialized();

        if (tileTypeId == _intactHullTileId)
        {
            stage = 0;
            return true;
        }

        for (var i = 0; i < _damagedHullTileIds.Length; i++)
        {
            if (tileTypeId != _damagedHullTileIds[i])
                continue;

            stage = i + 1;
            return true;
        }

        stage = 0;
        return false;
    }

    public bool TryGetNextDamageTile(int tileTypeId, out ushort nextTileTypeId)
    {
        EnsureInitialized();

        if (!TryGetDamageStage(tileTypeId, out var stage) || stage >= _damagedHullTileIds.Length)
        {
            nextTileTypeId = default;
            return false;
        }

        nextTileTypeId = _damagedHullTileIds[stage];
        return true;
    }

    public bool TryGetPreviousDamageTile(int tileTypeId, out ushort previousTileTypeId)
    {
        EnsureInitialized();

        if (!TryGetDamageStage(tileTypeId, out var stage) || stage <= 0)
        {
            previousTileTypeId = default;
            return false;
        }

        previousTileTypeId = stage == 1 ? _intactHullTileId : _damagedHullTileIds[stage - 2];
        return true;
    }

    public Tile WithTileType(Tile source, ushort tileTypeId)
    {
        EnsureInitialized();

        var variant = source.Variant;
        if (_tileDefinitions.TryGetDefinition(tileTypeId, out var tileDefinition) &&
            variant >= tileDefinition.Variants)
        {
            variant = 0;
        }

        return new Tile(tileTypeId, source.Flags, variant, source.RotationMirroring);
    }

    public int GetFloodContribution(int tileTypeId)
    {
        return TryGetDamageStage(tileTypeId, out var stage) ? stage : 0;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        if (!_tileDefinitions.TryGetDefinition(IntactHullTile, out var intactTile))
            throw new InvalidOperationException($"Ship hull tile '{IntactHullTile}' is not defined.");

        _intactHullTileId = intactTile.TileId;

        for (var i = 0; i < DamagedHullTiles.Length; i++)
        {
            if (!_tileDefinitions.TryGetDefinition(DamagedHullTiles[i], out var damagedTile))
                throw new InvalidOperationException($"Ship hull damage tile '{DamagedHullTiles[i]}' is not defined.");

            _damagedHullTileIds[i] = damagedTile.TileId;
        }

        _initialized = true;
    }
}
