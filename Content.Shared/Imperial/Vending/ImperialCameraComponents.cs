using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>
/// /obj/item/camera TG: снимок области (2×aperture−1)² клеток вокруг цели, вспышка, плёнка на 10 снимков,
/// фокус (Alt) и режим печати цвет/ч-б.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialPhotoCameraComponent : Component
{
    [DataField, AutoNetworkedField]
    public int PicturesLeft = 10;

    [DataField, AutoNetworkedField]
    public int PicturesMax = 10;

    /// <summary>picture_size_x/y: «половина ширины» апертуры, 1..4.</summary>
    [DataField, AutoNetworkedField]
    public int SizeX = 2;

    [DataField, AutoNetworkedField]
    public int SizeY = 2;

    public const int SizeLimit = 4;

    [DataField, AutoNetworkedField]
    public bool Monochrome;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(6.4);

    [DataField]
    public TimeSpan FlashDuration = TimeSpan.FromSeconds(0.2);

    [DataField]
    public float Range = 9f;

    [DataField]
    public SoundSpecifier SnapSound = new SoundCollectionSpecifier("ImperialPolaroid");

    [DataField]
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/click.ogg");

    [DataField]
    public string PhotoPrototype = "ImperialPhotograph";

    /// <summary>on: вспышка заряжена.</summary>
    [ViewVariables]
    public bool On = true;

    /// <summary>blending: идёт обработка снимка.</summary>
    [ViewVariables]
    public bool Blending;

    [ViewVariables]
    public int RequestId;

    [ViewVariables]
    public EntityUid? Photographer;

    [ViewVariables]
    public MapCoordinates Target;

    [ViewVariables]
    public Angle Rotation;

    [ViewVariables]
    public string Description = string.Empty;

    /// <summary>Последний снимок (PNG) — для администраторов.</summary>
    [ViewVariables]
    public byte[] LastImage = Array.Empty<byte>();
}

/// <summary>/obj/item/photo TG: снимок, подпись на обороте.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialPhotoComponent : Component
{
    /// <summary>PNG снимка; на клиент уходит только через окно просмотра.</summary>
    [ViewVariables]
    public byte[] Image = Array.Empty<byte>();

    /// <summary>Миниатюра 8×8 RGBA для значка (get_small_icon).</summary>
    [DataField, AutoNetworkedField]
    public byte[] Thumbnail = Array.Empty<byte>();

    /// <summary>scribble: надпись на обороте.</summary>
    [DataField, AutoNetworkedField]
    public string? Scribble;
}

[Serializable, NetSerializable]
public enum ImperialCameraVisuals : byte
{
    On,
}

[Serializable, NetSerializable]
public enum ImperialPhotoUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ImperialPhotoState(string name, byte[] image, string? scribble) : BoundUserInterfaceState
{
    public readonly string Name = name;
    public readonly byte[] Image = image;
    public readonly string? Scribble = scribble;
}

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

/// <summary>Клиент возвращает PNG снимка и миниатюру 8×8.</summary>
[Serializable, NetSerializable]
public sealed class ImperialCameraCaptureResponseEvent(int id, byte[] image, byte[] thumbnail) : EntityEventArgs
{
    public readonly int Id = id;
    public readonly byte[] Image = image;
    public readonly byte[] Thumbnail = thumbnail;
}
