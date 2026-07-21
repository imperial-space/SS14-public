using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.CartographerTable;

[Serializable, NetSerializable]
public sealed class MedievalCartographerBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly NetCoordinates Coordinates;
    public readonly Angle Angle;
    public readonly float MaxRange;
    public readonly bool RotateWithEntity;
    public readonly List<CartographerRadarMarkerData> RadarMarkers;

    public MedievalCartographerBoundUserInterfaceState(
        NetCoordinates coordinates,
        Angle angle,
        float maxRange,
        bool rotateWithEntity,
        List<CartographerRadarMarkerData> radarMarkers)
    {
        Coordinates = coordinates;
        Angle = angle;
        MaxRange = maxRange;
        RotateWithEntity = rotateWithEntity;
        RadarMarkers = radarMarkers;
    }
}

[Serializable, NetSerializable]
public struct CartographerRadarMarkerData
{
    public Vector2 Position;
    public Color Color;
    public float Size;
    public bool ZoomScaling;
    public string? RsiPath;
    public string? State;
}
