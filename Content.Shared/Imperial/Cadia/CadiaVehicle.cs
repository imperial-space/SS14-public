using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public enum CadiaVehicleSeat : byte
{
    Driver,
    Gunner,
    Passenger,
}

/// <summary>Оружие техники: снаряд, скорость, разброс, темп или перезарядка, боезапас и звук.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CadiaVehicleWeapon
{
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public EntProtoId Projectile;

    [DataField]
    public float Speed = 25f;

    /// <summary>Разброс выстрела в градусах.</summary>
    [DataField]
    public float Spread = 2f;

    /// <summary>Пауза между выстрелами (у пушки — перезарядка, у пулемёта — темп).</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(0.2);

    /// <summary>Стреляет, пока зажата кнопка.</summary>
    [DataField]
    public bool Automatic;

    [DataField]
    public int Ammo = 100;

    [DataField]
    public int MaxAmmo = 100;

    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>Встряска камеры экипажа при выстреле.</summary>
    [DataField]
    public float Kick;

    /// <summary>Каждый выстрел заряжается вручную: снаряд из боеукладки в казённик.</summary>
    [DataField]
    public bool Breech;

    /// <summary>Навесной огонь: снаряд падает на точку под прицелом, над ней загорается метка удара.</summary>
    [DataField]
    public bool Indirect;

    [DataField]
    public float IndirectRange = 28f;

    /// <summary>Сила взрыва навесного снаряда.</summary>
    [DataField]
    public float IndirectIntensity = 120f;
}

/// <summary>
/// Боевая машина Кадии: корпус, экипаж внутри (водитель, наводчик, десант), башня, вращающаяся отдельно от корпуса.
/// Водитель ведёт корпус своими кнопками движения: у танков W/S — ход вдоль корпуса, A/D — разворот на месте,
/// у шагохода — шаг в любую сторону с доворотом корпуса. Наводчик ведёт башню за курсором и стреляет ЛКМ.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaVehicleComponent : Component
{
    public const string DriverSlot = "cadia-driver";
    public const string GunnerSlot = "cadia-gunner";
    public const string PassengerContainer = "cadia-passengers";

    /// <summary>Прочность корпуса для табло экипажа (равна порогу разрушения в прототипе).</summary>
    [DataField]
    public float MaxIntegrity = 1000f;

    [DataField]
    public bool HasGunnerSeat = true;

    /// <summary>Есть ли в кабине кресло водителя: у ДОТа ездить некому и незачем.</summary>
    [DataField]
    public bool HasDriverSeat = true;

    /// <summary>Открытое орудие: садятся «за орудие», а не «за рычаги».</summary>
    [DataField]
    public bool GunPost;

    /// <summary>
    /// Машина с кабиной (Леман Расс, Химера): через люк попадают в кабину — отдельную карту с креслами водителя
    /// и наводчика, казёнником, боеукладкой и десантными скамьями. Иначе экипаж сидит прямо в корпусе (Часовой).
    /// </summary>
    [DataField]
    public bool Interior;

    /// <summary>Внутренний размер кабины (без стен).</summary>
    [DataField]
    public Vector2i InteriorSize = new(3, 3);

    [DataField]
    public int RackShells = 12;

    [DataField, AutoNetworkedField]
    public bool Locked;

    /// <summary>Снаряд в казённике основного орудия.</summary>
    [DataField, AutoNetworkedField]
    public bool Loaded = true;

    /// <summary>Снарядов в боеукладке (для табло).</summary>
    [DataField, AutoNetworkedField]
    public int RackCount;

    [ViewVariables]
    public EntityUid? InteriorMap;

    /// <summary>Какой вокс-динамик ставить в кабину: у Хаоса своя сеть.</summary>
    [DataField]
    public EntProtoId InteriorSpeaker = "CadiaVehicleSpeaker";

    /// <summary>Приближение камеры наводчика: у артиллерии обзор шире.</summary>
    [DataField]
    public float GunnerZoom = 1f;

    /// <summary>Куда смотрит прицел (только на сервере, для навесного огня).</summary>
    [ViewVariables]
    public Robust.Shared.Map.MapCoordinates AimPoint;

    /// <summary>Дуло шагохода по направлениям (тайлы от центра): пушка на руке, а не в центре.</summary>
    [DataField]
    public Dictionary<Direction, Vector2> Muzzles = new();

    /// <summary>Водитель сам и стреляет (Часовой).</summary>
    [DataField]
    public bool DriverIsGunner;

    [DataField]
    public int Passengers;

    /// <summary>Гусеничное управление (иначе — шагоход).</summary>
    [DataField]
    public bool TankControls = true;

    [DataField]
    public float Speed = 3f;

    [DataField]
    public float ReverseSpeed = 1.6f;

    /// <summary>Скорость разворота корпуса, градусов в секунду.</summary>
    [DataField]
    public float TurnRate = 70f;

    [DataField]
    public bool HasTurret = true;

    [DataField]
    public float TurretTurnRate = 60f;

    /// <summary>У шагохода без башни — в каком секторе от носа может стрелять.</summary>
    [DataField]
    public float FireArc = 60f;

    /// <summary>Расстояние от центра до среза ствола.</summary>
    [DataField]
    public float MuzzleOffset = 1.7f;

    [DataField]
    public List<CadiaVehicleWeapon> Weapons = new();

    [DataField, AutoNetworkedField]
    public int SelectedWeapon;

    /// <summary>Боезапас по оружию (сетевой — для табло экипажа).</summary>
    [DataField, AutoNetworkedField]
    public List<int> Ammo = new();

    [DataField, AutoNetworkedField]
    public List<TimeSpan> NextShot = new();

    /// <summary>Мировой угол башни (0 — стволы на север).</summary>
    [DataField, AutoNetworkedField]
    public Angle TurretAngle;

    [DataField, AutoNetworkedField]
    public Angle TurretTarget;

    [DataField, AutoNetworkedField]
    public bool Firing;

    [DataField, AutoNetworkedField]
    public EntityUid? Driver;

    [DataField, AutoNetworkedField]
    public EntityUid? Gunner;

    [DataField, AutoNetworkedField]
    public int PassengerCount;

    [DataField, AutoNetworkedField]
    public int SmokeCharges = 3;

    [DataField]
    public int MaxSmokeCharges = 3;

    [DataField]
    public TimeSpan SmokeCooldown = TimeSpan.FromSeconds(25);

    [DataField, AutoNetworkedField]
    public TimeSpan NextSmoke;

    [DataField]
    public EntProtoId SmokeCloud = "CadiaSmokeCloud";

    [DataField]
    public SoundSpecifier EngineStartSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehiclestartup.ogg");

    [DataField]
    public SoundSpecifier EnterSound = new SoundPathSpecifier("/Audio/Machines/airlock_ext_open.ogg");

    [DataField]
    public SoundSpecifier SmokeSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/grenade_launcher.ogg");

    [DataField]
    public EntProtoId Wreck = "CadiaVehicleWreck";

    [DataField]
    public EntProtoId ExitAction = "ActionCadiaVehicleExit";

    [DataField]
    public EntProtoId SwitchAction = "ActionCadiaVehicleSwitch";

    [DataField]
    public EntProtoId SmokeAction = "ActionCadiaVehicleSmoke";
}

/// <summary>Боец внутри машины: где сидит. По нему клиент рисует табло экипажа и прицел.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaVehicleCrewComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Vehicle;

    [DataField, AutoNetworkedField]
    public CadiaVehicleSeat Seat;

    [ViewVariables]
    public List<EntityUid> Actions = new();
}

/// <summary>Наводчик ведёт башню за курсором.</summary>
[Serializable, NetSerializable]
public sealed class CadiaVehicleAimEvent(NetEntity vehicle, NetCoordinates target) : EntityEventArgs
{
    public readonly NetEntity Vehicle = vehicle;
    public readonly NetCoordinates Target = target;
}

/// <summary>Спуск нажат или отпущен.</summary>
[Serializable, NetSerializable]
public sealed class CadiaVehicleFireEvent(NetEntity vehicle, bool down) : EntityEventArgs
{
    public readonly NetEntity Vehicle = vehicle;
    public readonly bool Down = down;
}

public sealed partial class CadiaVehicleExitActionEvent : InstantActionEvent;

public sealed partial class CadiaVehicleSwitchActionEvent : InstantActionEvent;

public sealed partial class CadiaVehicleSmokeActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class CadiaVehicleRitualDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CadiaVehicleEnterDoAfterEvent : DoAfterEvent
{
    [DataField]
    public CadiaVehicleSeat Seat;

    public CadiaVehicleEnterDoAfterEvent(CadiaVehicleSeat seat)
    {
        Seat = seat;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>Кресло в кабине: пристегнулся — занял место водителя или наводчика, камера уходит наружу.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaVehicleSeatComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [DataField, AutoNetworkedField]
    public CadiaVehicleSeat Seat;
}

/// <summary>Ключ от машины: запирает и отпирает её люк снаружи и изнутри.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaVehicleKeyComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;
}

/// <summary>Люк кабины: выводит наружу.</summary>
[RegisterComponent]
public sealed partial class CadiaVehicleHatchComponent : Component
{
    [ViewVariables]
    public EntityUid? Vehicle;
}

/// <summary>Казённик основного орудия: в него вкладывают снаряд.</summary>
[RegisterComponent]
public sealed partial class CadiaVehicleBreechComponent : Component
{
    [ViewVariables]
    public EntityUid? Vehicle;
}

/// <summary>Боеукладка: выдаёт снаряды по одному, принимает обратно.</summary>
[RegisterComponent]
public sealed partial class CadiaAmmoRackComponent : Component
{
    [ViewVariables]
    public EntityUid? Vehicle;
}

/// <summary>Снаряд основного орудия.</summary>
[RegisterComponent]
public sealed partial class CadiaTankShellComponent : Component;

/// <summary>Карта-кабина машины.</summary>
[RegisterComponent]
public sealed partial class CadiaVehicleInteriorComponent : Component
{
    [ViewVariables]
    public EntityUid? Vehicle;
}

[Serializable, NetSerializable]
public enum CadiaVehicleVisuals : byte
{
    Loaded,
    RackFull,
}

[Serializable, NetSerializable]
public sealed partial class CadiaBreechLoadDoAfterEvent : SimpleDoAfterEvent;