using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Голокарта поверхности под кораблём: плитки окна вокруг точки под кораблём (0 — пусто, 1 — земля, 2 — стена/постройка)
/// и отметки. Окно квадратное, <see cref="Size"/>×<see cref="Size"/>, левый нижний угол — <see cref="Origin"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class CadiaBatteryBuiState : BoundUserInterfaceState
{
    public CadiaBatteryKind Kind;
    public bool InOrbit;
    public string? Denial;
    public Vector2i Origin;
    public int Size;
    public byte[] Tiles = Array.Empty<byte>();
    public List<CadiaHoloMarker> Markers = new();
    public Vector2 ShipPosition;
    public float Range;
    public float Spread;
    public int Ammo;
    public int MaxAmmo;
    public TimeSpan NextShot;
    public Vector2? Target;
}
