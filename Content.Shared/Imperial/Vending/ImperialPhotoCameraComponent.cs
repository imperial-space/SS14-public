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
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Imperial/Machines/click.ogg");

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

    /// <summary>picture.mobs_seen и dead_seen последнего снимка.</summary>
    [ViewVariables]
    public List<EntityUid> MobsSeen = new();

    [ViewVariables]
    public List<EntityUid> DeadSeen = new();

    /// <summary>Последний снимок (PNG) — для администраторов.</summary>
    [ViewVariables]
    public byte[] LastImage = Array.Empty<byte>();
}
