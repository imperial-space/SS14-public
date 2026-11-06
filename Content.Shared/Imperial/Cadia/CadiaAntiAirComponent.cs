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

    [DataField]
    public TimeSpan TurnSoundDelay = TimeSpan.FromSeconds(1.2);
}
