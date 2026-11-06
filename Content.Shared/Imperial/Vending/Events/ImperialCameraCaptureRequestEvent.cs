using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>Сервер просит клиент фотографа отрисовать область.</summary>
[Serializable, NetSerializable]
public sealed class ImperialCameraCaptureRequestEvent(int id, MapCoordinates center, Angle rotation, int width, int height, bool monochrome) : EntityEventArgs
{
    public readonly Angle Rotation = rotation;
    public readonly int Id = id;
    public readonly MapCoordinates Center = center;
    public readonly int Width = width;
    public readonly int Height = height;
    public readonly bool Monochrome = monochrome;
}
