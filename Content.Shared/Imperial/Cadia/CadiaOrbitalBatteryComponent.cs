using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Консоль орбитальной батареи на корабле. Стреляет только с орбиты над стопкой Кадии: цель выбирается
/// на голокарте поверхности под кораблём, за несколько секунд до удара на земле загорается красный круг.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaOrbitalBatteryComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaBatteryKind Kind = CadiaBatteryKind.Macro;

    /// <summary>Снарядов в погребе (у лэнса не тратятся).</summary>
    [DataField, AutoNetworkedField]
    public int Ammo = 6;

    [DataField, AutoNetworkedField]
    public int MaxAmmo = 12;

    /// <summary>Снарядов в одном залпе макропушек.</summary>
    [DataField]
    public int Salvo = 3;

    [DataField]
    public TimeSpan Reload = TimeSpan.FromSeconds(45);

    [DataField, AutoNetworkedField]
    public TimeSpan NextShot;

    /// <summary>Разброс попаданий вокруг цели (в тайлах).</summary>
    [DataField]
    public float Spread = 4f;

    /// <summary>Насколько далеко от точки под кораблём можно навести (в тайлах).</summary>
    [DataField]
    public float Range = 60f;

    /// <summary>Сколько горит предупреждающий круг до удара.</summary>
    [DataField]
    public TimeSpan Warning = TimeSpan.FromSeconds(7);

    [DataField]
    public float Intensity = 260f;

    [DataField]
    public float Slope = 4f;

    [DataField]
    public float MaxTileIntensity = 30f;

    [DataField]
    public EntProtoId Ammunition = "CadiaMacroShell";

    [DataField]
    public SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/Imperial/ElysiumPrime/Music/X4Artillery.ogg");

    [DataField]
    public SoundSpecifier LoadSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/batrifle_magin.ogg");

    /// <summary>Выбранная цель в мировых координатах поверхности.</summary>
    [DataField, AutoNetworkedField]
    public Vector2? Target;
}
