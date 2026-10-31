using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Зенитная позиция 4×4: снаружи — бетонный капонир с вращающейся башней, внутри — тесный бункер 3×3
/// с огневой консолью и вокс-динамиком. Наводчик в бункере выбирает точку в небе на радаре, башня
/// поворачивается к ней с конечной скоростью и стреляет, только когда довернула.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaAntiAirComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaFaction Faction = CadiaFaction.Imperium;

    /// <summary>Дальность по горизонтали (в тайлах).</summary>
    [DataField]
    public float Range = 24f;

    /// <summary>На сколько этажей вверх достаёт (1 — только небо, 2 — и орбита).</summary>
    [DataField]
    public int Levels = 1;

    [DataField]
    public TimeSpan FireRate = TimeSpan.FromSeconds(0.6);

    /// <summary>Разброс разрыва на предельной дальности (в тайлах); ближе — точнее.</summary>
    [DataField]
    public float Spread = 3f;

    /// <summary>Скорость полёта снаряда (тайлов в секунду) — по ней считается задержка разрыва.</summary>
    [DataField]
    public float ShellSpeed = 40f;

    /// <summary>Сила взрыва на корпусе при попадании.</summary>
    [DataField]
    public float Damage = 14f;

    /// <summary>Шанс, что попадание придётся прямо в двигатель.</summary>
    [DataField]
    public float ThrusterHitChance = 0.25f;

    /// <summary>Скорость поворота башни, градусов в секунду.</summary>
    [DataField]
    public float TurnRate = 70f;

    /// <summary>Снарядов в ленте; ноль — нужна перезарядка снарядными ящиками.</summary>
    [DataField, AutoNetworkedField]
    public int Ammo = 200;

    [DataField, AutoNetworkedField]
    public int MaxAmmo = 200;

    [DataField]
    public SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/lmg.ogg");

    [DataField]
    public SoundSpecifier TurnSound = new SoundPathSpecifier("/Audio/Machines/airlock_creaking.ogg");

    [DataField]
    public EntProtoId MuzzleEffect = "CadiaFlakMuzzle";

    [DataField]
    public EntProtoId BurstEffect = "CadiaFlakBurst";

    /// <summary>Внутреннее помещение позиции и что в нём ставить.</summary>
    [DataField]
    public EntProtoId InteriorConsole = "CadiaAntiAirConsole";

    [DataField]
    public EntProtoId InteriorSpeaker = "CadiaVoxSpeaker";

    /// <summary>Текущий угол башни (0 — стволы на север). Его видят клиенты и крутят слой башни.</summary>
    [DataField, AutoNetworkedField]
    public Angle Aim;

    /// <summary>Куда наводчик повернул башню.</summary>
    [DataField, AutoNetworkedField]
    public Angle TargetAim;

    /// <summary>Точка прицеливания относительно позиции (в тайлах).</summary>
    [DataField, AutoNetworkedField]
    public Vector2? AimPoint;

    /// <summary>Этаж выше, по которому бьём (1 — небо, 2 — орбита).</summary>
    [DataField, AutoNetworkedField]
    public int AimLevel = 1;

    [DataField, AutoNetworkedField]
    public bool Firing;

    /// <summary>Захваченный борт и точка на нём (локальные координаты сетки) — прицел ведёт её, пока борт летит.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? AimGrid;

    [DataField, AutoNetworkedField]
    public Vector2 AimLocal;

    /// <summary>Захваченный борт ушёл за предел дальности — огонь невозможен.</summary>
    [DataField, AutoNetworkedField]
    public bool OutOfRange;

    [DataField]
    public TimeSpan NextShot;

    [ViewVariables]
    public EntityUid? Interior;

    [ViewVariables]
    public EntityUid? InteriorConsoleEntity;

    [ViewVariables]
    public TimeSpan NextTurnSound;
}

/// <summary>Огневая консоль в бункере зенитной позиции.</summary>
[RegisterComponent]
public sealed partial class CadiaAntiAirConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? Gun;
}

/// <summary>Люк бункера: выводит наружу к своей позиции.</summary>
[RegisterComponent]
public sealed partial class CadiaEmplacementHatchComponent : Component
{
    [ViewVariables]
    public EntityUid? Gun;
}

/// <summary>Карта-бункер зенитной позиции.</summary>
[RegisterComponent]
public sealed partial class CadiaEmplacementInteriorComponent : Component
{
    [ViewVariables]
    public EntityUid? Gun;
}

/// <summary>Снарядный ящик: пополняет ленту зенитки.</summary>
[RegisterComponent]
public sealed partial class CadiaFlakAmmoComponent : Component
{
    [DataField]
    public int Rounds = 100;
}

[Serializable, NetSerializable]
public enum CadiaAntiAirUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CadiaAirContactKind : byte
{
    Enemy,
    Friendly,
    Neutral,
}

/// <summary>Отметка на радаре: борт над позицией (смещение от позиции в тайлах, этаж, размер).</summary>
[Serializable, NetSerializable]
public readonly record struct CadiaAirContact(NetEntity Grid, Vector2 Offset, int Level, float Size, CadiaAirContactKind Kind, string Name, int SilhouetteWidth, int SilhouetteHeight, byte[] Silhouette);

/// <summary>Разрыв последних секунд на радаре: попал или нет.</summary>
[Serializable, NetSerializable]
public readonly record struct CadiaAirBurst(Vector2 Offset, int Level, bool Hit);

[Serializable, NetSerializable]
public sealed class CadiaAntiAirBuiState : BoundUserInterfaceState
{
    public string GunName = string.Empty;
    public bool Linked;
    public float Range;
    public int Levels;
    public int AimLevel;
    public Angle Aim;
    public Angle TargetAim;
    public Vector2? AimPoint;
    public bool Firing;
    public int Ammo;
    public int MaxAmmo;
    public bool AuspexLinked;
    public List<CadiaAirContact> Contacts = new();
    public List<CadiaAirBurst> Bursts = new();

    /// <summary>Борт, на котором захвачена точка прицеливания, и в дальности ли он.</summary>
    public NetEntity? AimGrid;
    public bool OutOfRange;

    /// <summary>Борт, за которым сейчас следит камера наводчика.</summary>
    public NetEntity? Viewing;
}

/// <summary>Навести камеру наводчика на борт (null — вернуть камеру в бункер).</summary>
[Serializable, NetSerializable]
public sealed class CadiaAntiAirViewMessage(NetEntity? grid) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Grid = grid;
}

/// <summary>Захватить точку на корпусе просматриваемого борта (координаты клика в мире).</summary>
[Serializable, NetSerializable]
public sealed class CadiaAntiAirLockMessage(NetCoordinates point) : BoundUserInterfaceMessage
{
    public readonly NetCoordinates Point = point;
}

/// <summary>
/// Наводчик смотрит на борт через камеру зенитки: курсор — прицел, существа на борту скрыты.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaAntiAirViewerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Console;

    [DataField, AutoNetworkedField]
    public EntityUid? Gun;

    [DataField, AutoNetworkedField]
    public EntityUid? Ship;

    [ViewVariables]
    public bool HadFov;
}

[Serializable, NetSerializable]
public sealed class CadiaAntiAirAimMessage(Vector2 offset) : BoundUserInterfaceMessage
{
    public readonly Vector2 Offset = offset;
}

[Serializable, NetSerializable]
public sealed class CadiaAntiAirLevelMessage(int level) : BoundUserInterfaceMessage
{
    public readonly int Level = level;
}

[Serializable, NetSerializable]
public sealed class CadiaAntiAirFireMessage(bool firing) : BoundUserInterfaceMessage
{
    public readonly bool Firing = firing;
}

/// <summary>
/// Ауспекс-радар: видит шаттлы врага на этажах выше, предупреждает свою сторону о новых целях
/// и увеличивает дальность обзора и точность зениток рядом.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaAuspexComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaFaction Faction = CadiaFaction.Imperium;

    [DataField]
    public float Range = 40f;

    /// <summary>Зенитки в этом радиусе получают данные наведения.</summary>
    [DataField]
    public float LinkRange = 14f;

    /// <summary>Во сколько раз уменьшается разброс связанной зенитки.</summary>
    [DataField]
    public float SpreadFactor = 0.6f;

    [DataField]
    public SoundSpecifier ContactSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/radar_ping.ogg");

    [ViewVariables]
    public HashSet<EntityUid> Contacts = new();

    [DataField, AutoNetworkedField]
    public int ContactCount;
}
