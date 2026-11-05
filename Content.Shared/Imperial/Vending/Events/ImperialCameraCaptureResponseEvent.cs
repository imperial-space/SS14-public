using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>Клиент возвращает PNG снимка и миниатюру 8×8.</summary>
[Serializable, NetSerializable]
public sealed class ImperialCameraCaptureResponseEvent(int id, byte[] image, byte[] thumbnail) : EntityEventArgs
{
    public readonly int Id = id;
    public readonly byte[] Image = image;
    public readonly byte[] Thumbnail = thumbnail;
}
