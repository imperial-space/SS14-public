using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Плитки этажа для голокарты (0 — пусто, 1 — пол, 2 — стена/постройка, 3 — дверь).</summary>
[Serializable, NetSerializable]
public sealed class CadiaHoloTilesMessage : BoundUserInterfaceMessage
{
    public CadiaFlightLevel Level;
    public Vector2i Origin;
    public int Width;
    public int Height;
    public byte[] Tiles = Array.Empty<byte>();
}
