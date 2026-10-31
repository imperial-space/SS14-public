using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

// ═══════════════════════ Орбитальные батареи ═══════════════════════

[Serializable, NetSerializable]
public enum CadiaBatteryKind : byte
{
    /// <summary>Макропушки: залп из нескольких снарядов с разбросом, нужны снаряды.</summary>
    Macro,

    /// <summary>Лэнс: один точный сверхмощный луч, долгая перезарядка, без снарядов.</summary>
    Lance,
}

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

[Serializable, NetSerializable]
public enum CadiaBatteryUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CadiaHoloMarkerKind : byte
{
    Ship,
    Friendly,
    Pylon,
    PylonFallen,
    Citadel,
    Strike,
    Target,
    Shuttle,

    /// <summary>Цель, отмеченная командиром на гололитическом столе.</summary>
    Designated,
}

[Serializable, NetSerializable]
public readonly record struct CadiaHoloMarker(CadiaHoloMarkerKind Kind, Vector2 Position);

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

[Serializable, NetSerializable]
public sealed class CadiaBatterySetTargetMessage(Vector2 target) : BoundUserInterfaceMessage
{
    public readonly Vector2 Target = target;
}

[Serializable, NetSerializable]
public sealed class CadiaBatteryFireMessage : BoundUserInterfaceMessage;

/// <summary>Снаряд макропушки: вкладывается в консоль батареи.</summary>
[RegisterComponent]
public sealed partial class CadiaMacroShellComponent : Component
{
    [DataField]
    public int Rounds = 1;
}

/// <summary>
/// Падающий орбитальный удар: на земле горит круг-предупреждение, по истечении времени — взрыв или луч лэнса.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaOrbitalStrikeComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Impact;

    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public CadiaBatteryKind Kind;

    [DataField]
    public float Intensity;

    [DataField]
    public float Slope;

    [DataField]
    public float MaxTileIntensity;

    [DataField]
    public EntityUid? Source;
}

// ═══════════════════════ Пустотный щит ═══════════════════════

/// <summary>
/// Пустотный щит корабля: поглощает орбитальные удары и попадания ПВО по своей сетке, пока не иссякнет,
/// затем гаснет на время перезапуска.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaVoidShieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Strength = 100f;

    [DataField, AutoNetworkedField]
    public float MaxStrength = 100f;

    /// <summary>Восстановление в секунду, пока щит поднят.</summary>
    [DataField]
    public float Regen = 1.5f;

    [DataField]
    public TimeSpan RebootTime = TimeSpan.FromSeconds(60);

    [DataField, AutoNetworkedField]
    public TimeSpan? RebootAt;

    [DataField]
    public SoundSpecifier AbsorbSound = new SoundPathSpecifier("/Audio/Imperial/MiningWeapons/Smasher/activate_shield.ogg");

    [DataField]
    public SoundSpecifier CollapseSound = new SoundPathSpecifier("/Audio/Imperial/ModularShield/empulse.ogg");

    [DataField]
    public SoundSpecifier RebootSound = new SoundPathSpecifier("/Audio/Imperial/MiningWeapons/Smasher/start_charging_shield.ogg");

    /// <summary>
    /// Радиус купола в клетках. 0 — щит корабля: прикрывает всю свою сетку. У щита на земле сетка — всё поле,
    /// поэтому он прикрывает только круг вокруг себя.
    /// </summary>
    [DataField]
    public float Radius;
}

[Serializable, NetSerializable]
public enum CadiaVoidShieldVisuals : byte
{
    Up,
}

