using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>
/// Ядерный реактор газового охлаждения NGCR-5600 из SS220 Paradise (code/modules/power/engines/fission).
/// Ядро 3×3; камеры со стержнями ставятся вплотную к ядру и друг к другу, газ подаётся газовыми узлами.
/// </summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionReactorComponent : Component
{
    public const float TotalControlRods = 5;
    public const int MinChambersToOverload = 20;
    public const float EventModifier = 0.5f;
    public const float HeatCap = 40000;
    public const float AverageHeatThreshold = 50;
    public const float TotalHeatThreshold = 600;
    public const float HeatConversionRatio = 400;
    public const float ReactivityCoefficientCap = 30;
    public const float HeatModifier = 450;

    public const float MeltdownPercent = 5;
    public const float EmergencyPercent = 25;
    public const float DangerPercent = 50;
    public const float WarningPercent = 99;
    public const float CriticalTemperature = 10000;
    public const float WarningPoint = 50;
    public const float EmergencyPoint = 700;
    public const float MeltdownPoint = 1000;

    public const int CountdownSeconds = 30;
    public static readonly TimeSpan WarningDelay = TimeSpan.FromSeconds(60);

    public const float HeatDamageRate = 500;
    public const float MolMinimum = 30;
    public const float PressureMaximum = 20000;
    public const float PressureDamage = 0.5f;
    public const float DamageMinimum = 0.002f;
    public const float DamageMaximum = 8;
    public const float ExplosionModifier = 1.5f;

    public const float MoleBonusThreshold = 800;
    public const float MoleBonusComponent = 250;

    public static readonly TimeSpan ProcessInterval = TimeSpan.FromSeconds(2);

    /// <summary>Газ теплоносителя в активной зоне.</summary>
    [DataField]
    public GasMixture Air = new(1000);

    /// <summary>Газ-замедлитель (отдельный объём, съедается каждый цикл).</summary>
    [DataField]
    public GasMixture ModeratorGas = new(500);

    [ViewVariables]
    public HashSet<EntityUid> ConnectedChambers = new();

    [ViewVariables] public bool CanCreatePower;
    [ViewVariables] public float FinalHeat;
    [ViewVariables] public float FinalPower;
    [ViewVariables] public float Reactivity = 1;
    [ViewVariables] public int ControlRodsRemaining = 5;
    [ViewVariables] public FissionRepairStep RepairStep = FissionRepairStep.Digging;
    [ViewVariables] public float DesiredPower;
    [ViewVariables] public float OperatingPower;
    [ViewVariables] public float Damage;
    [ViewVariables] public bool StartingUp = true;
    [ViewVariables] public bool Offline = true;
    [ViewVariables] public float HeatDamageThreshold = 1000;
    [ViewVariables] public float AverageHeatgen;
    [ViewVariables] public TimeSpan LastWarning;
    [ViewVariables] public bool SendMessage;
    [ViewVariables] public bool FinalCountdown;
    [ViewVariables(VVAccess.ReadWrite)] public bool AdminIntervention;
    [ViewVariables] public bool SafetyOverride;
    [ViewVariables] public bool ControlLockout;
    [ViewVariables] public bool Venting;
    [ViewVariables] public bool VentLockout;
    [ViewVariables] public float MinimumOperatingTemp;
    [ViewVariables] public bool Broken;
    [ViewVariables] public bool ActiveMeltdown;

    [ViewVariables] public float GasOverheatBonus;
    [ViewVariables] public float GasReactivityBonus;
    [ViewVariables] public float GasEventModifier = 1;
    [ViewVariables] public float GasControlMod;
    [ViewVariables] public float GasRadiationMod = 1;
    [ViewVariables] public float GasPermeabilityMod;
    [ViewVariables] public float GasPowerMod = 1;
    [ViewVariables] public float GasDepletionMod;
    [ViewVariables] public bool GasIsFueled;
    [ViewVariables] public float GasFuelPower;
    [ViewVariables] public float GasFuelMoles;
    [ViewVariables] public float GasFuelHeat;
    [ViewVariables] public float GasAbsorptionConstant = 0.5f;
    [ViewVariables] public float GasAbsorptionEffectiveness = 0.5f;

    [DataField] public float PressureCriticalThreshold = 10000;
    [DataField] public float PressureWarningThreshold = 6895;
    [DataField] public float PressureDamageRate = 2;

    [ViewVariables] public TimeSpan NextProcess;
    [ViewVariables] public TimeSpan NextVent;
    [ViewVariables] public int CountdownRemaining;
    [ViewVariables] public TimeSpan NextCountdownStep;
    [ViewVariables] public TimeSpan? OverloadDetonateAt;
    [ViewVariables] public TimeSpan? ScramUntil;
    [ViewVariables] public TimeSpan? OverloadPrepAt;

    /// <summary>Звуковое сопровождение SCRAM: power_fraction и temp_fraction из scram().</summary>
    [ViewVariables] public float ScramPowerFraction;
    [ViewVariables] public float ScramTempFraction;

    [DataField] public SoundSpecifier ShutoffSound = new SoundPathSpecifier("/Audio/Imperial/Fission/reactor_shutoff.ogg");
    [DataField] public SoundSpecifier StartupSound = new SoundPathSpecifier("/Audio/Imperial/Fission/reactor_startup.ogg");
    [DataField] public SoundSpecifier StartupBeginningSound = new SoundPathSpecifier("/Audio/Imperial/Fission/reactor_startup_beginning.ogg");
    [DataField] public SoundSpecifier StartupLoopSound = new SoundPathSpecifier("/Audio/Imperial/Fission/reactor_startup_mid.ogg");
    [DataField] public SoundSpecifier LoopSound = new SoundPathSpecifier("/Audio/Imperial/Fission/reactor_loop.ogg");
    [DataField] public SoundSpecifier MeltdownSound = new SoundPathSpecifier("/Audio/Imperial/Fission/meltdown.ogg");
    [DataField] public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Machines/Nuke/angry_beep.ogg");
    [DataField] public SoundSpecifier AlarmSound = new SoundPathSpecifier("/Audio/Machines/alarm.ogg");
    [DataField] public SoundSpecifier ControlRodFailSound = new SoundCollectionSpecifier("MetalSlam");
    [DataField] public SoundSpecifier SteamSound = new SoundPathSpecifier("/Audio/Effects/spray.ogg");
    [DataField] public SoundSpecifier HissSound = new SoundPathSpecifier("/Audio/Effects/spray2.ogg");

    [DataField] public EntProtoId Slag = "ImperialFissionSlag";
    [DataField] public EntProtoId Terminal = "ImperialFissionPowerTerminal";
    [DataField] public EntProtoId Waste = "ImperialFissionWaste";
    [DataField] public EntProtoId WasteEpicenter = "ImperialFissionWasteEpicenter";
    [DataField] public EntProtoId RadiationPulse = "ImperialFissionRadiationPulse";
    [DataField] public EntProtoId Smoke = "ImperialFissionSmoke";
    [DataField] public EntProtoId Fallout = "WeatherFallout";
}

/// <summary>Камера для стержня (reactor_chamber).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionChamberComponent : Component
{
    public const string ContainerId = "fission_rod";
    public const float HeatDamage = 15;

    [ViewVariables] public EntityUid? LinkedReactor;
    [ViewVariables] public FissionChamberState State = FissionChamberState.Down;
    [ViewVariables] public bool RequirementsMet;
    [ViewVariables] public bool Operational;
    [ViewVariables] public List<EntityUid> Neighbors = new();
    [ViewVariables] public float HeatTotal;
    [ViewVariables] public float PowerTotal;
    [ViewVariables] public bool Enriching;
    [ViewVariables] public float PowerModTotal = 1;
    [ViewVariables] public float HeatModTotal = 1;
    [ViewVariables] public TimeSpan LockoutUntil;
    [ViewVariables] public int DurabilityLevel;
    [ViewVariables] public bool Welded;
    [ViewVariables] public bool PanelOpen;

    /// <summary>Стержень, вставленный при спавне (uranium / heavy_water подтипы).</summary>
    [DataField] public EntProtoId? StartingRod;

    [DataField] public SoundSpecifier MoveSound = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");
    [DataField] public SoundSpecifier SwitchSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");
    [DataField] public SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/Machines/door_lock_on.ogg");
    [DataField] public SoundSpecifier RemoveSound = new SoundPathSpecifier("/Audio/Machines/door_lock_off.ogg");
    [DataField] public SoundSpecifier WeldSound = new SoundCollectionSpecifier("Welder");
    [DataField] public SoundSpecifier BangSound = new SoundPathSpecifier("/Audio/Effects/explosion_small1.ogg");
    [DataField] public EntProtoId EjectedRod = "ImperialFissionEjectedRod";
}

/// <summary>Ядерный стержень: топливо, замедлитель или охладитель (nuclear_rods.dm).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionRodComponent : Component
{
    [DataField(required: true)] public FissionRodCategory Category;

    /// <summary>Отрицательное значение — бесконечная прочность (INFINITY в SS13).</summary>
    [DataField] public float MaxDurability = 3000;
    [DataField] public float Durability = -1;

    [DataField] public float HeatAmount;
    [DataField] public float HeatAmpMod = 1;
    [DataField] public float PowerAmount;
    [DataField] public float PowerAmpMod = 1;
    [ViewVariables] public float CurrentHeatMod = 1;
    [ViewVariables] public float CurrentPowerMod = 1;

    /// <summary>
    /// Требования к соседям: "Fuel"/"Moderator"/"Coolant" — любой стержень категории,
    /// иначе id прототипа конкретного стержня.
    /// </summary>
    [DataField] public List<string> Requirements = new();

    [DataField] public float MinimumTempModifier;
    [DataField] public float ReactorOverheatModifier;

    /// <summary>Сила излучения вне камеры (radiation_range/chance).</summary>
    [DataField] public float RadiationIntensity;

    [DataField] public int EnrichmentCycles = 25;
    [DataField] public float PowerEnrichThreshold;
    [DataField] public float HeatEnrichThreshold;
    [DataField] public EntProtoId? PowerEnrichResult;
    [DataField] public EntProtoId? HeatEnrichResult;
    [ViewVariables] public int PowerEnrichProgress;
    [ViewVariables] public int HeatEnrichProgress;

    /// <summary>Банановый стержень: случайные характеристики при создании.</summary>
    [DataField] public bool RandomStats;

    /// <summary>Можно ли изготовить в фабрикаторе (craftable).</summary>
    [DataField] public bool Craftable;

    /// <summary>Нужен научный диск фабрикатора (upgrade_required).</summary>
    [DataField] public bool UpgradeRequired;

    /// <summary>Материалы для фабрикатора, единицы SS14 (100 = лист).</summary>
    [DataField] public Dictionary<string, int> Materials = new();

    public bool Infinite => MaxDurability < 0;
}

/// <summary>Газовый узел реактора: подача, откачка или замедлитель (reactor_gas_node).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionGasNodeComponent : Component
{
    public const float MinimumMoles = 3;

    [DataField] public bool Moderator;
    [DataField] public bool Intake = true;
    [DataField] public string NodeName = "pipe";
    [DataField] public float TargetPressure = 100000;
    [ViewVariables] public EntityUid? LinkedReactor;
    [DataField] public SoundSpecifier PingSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");
    [DataField] public SoundSpecifier BuzzSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
}

/// <summary>Выходной силовой терминал реактора (reactor_power).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionPowerTerminalComponent : Component
{
    [ViewVariables] public EntityUid? Reactor;
}

/// <summary>Консоль наблюдения NGCR (fission_monitor).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionMonitorComponent : Component
{
    [ViewVariables] public EntityUid? Reactor;
    [ViewVariables] public bool Controller = true;
    [DataField] public float ControlRange = 12;
    [ViewVariables] public TimeSpan NextUpdate;
    [DataField] public SoundSpecifier BuzzSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
}

/// <summary>Центрифуга обогащения топлива (nuclear_centrifuge).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionCentrifugeComponent : Component
{
    public const string ContainerId = "fission_centrifuge";

    [DataField] public TimeSpan WorkTime = TimeSpan.FromSeconds(10);
    [ViewVariables] public bool Active;
    [ViewVariables] public TimeSpan EndTime;
    [ViewVariables] public EntProtoId? Result;
    [ViewVariables] public EntityUid? PendingRod;
    [ViewVariables] public TimeSpan? UnpoweredSince;
    [DataField] public float IdleLoad = 200;
    [DataField] public float ActiveLoad = 3000;
    [DataField] public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/Imperial/Fission/centrifuge_start.ogg");
    [DataField] public SoundSpecifier PingSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");
    [DataField] public SoundSpecifier BuzzSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
    [DataField] public SoundSpecifier EjectSound = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");
}

/// <summary>Фабрикатор ядерных стержней (nuclear_rod_fabricator): чертежи из прототипов стержней, 10 секунд на стержень.</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionFabricatorComponent : Component
{
    [DataField] public bool Upgraded;
    [DataField] public EntProtoId UpgradeDisk = "ImperialFissionFabricatorUpgrade";
    [DataField] public TimeSpan WorkTime = TimeSpan.FromSeconds(10);
    [DataField] public float IdleLoad = 50;
    [DataField] public float ActiveLoad = 3000;
    [ViewVariables] public bool Active;
    [ViewVariables] public TimeSpan EndTime;
    [ViewVariables] public EntProtoId? Schematic;
    [ViewVariables] public TimeSpan? UnpoweredSince;
    [DataField] public SoundSpecifier PingSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");
    [DataField] public SoundSpecifier BuzzSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
}

[RegisterComponent]
public sealed partial class FissionFabricatorUpgradeComponent : Component;

/// <summary>Плутониевая жижа (nuclear_waste): лопатой убирается за 5 секунд.</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionWasteComponent : Component
{
    [DataField] public SoundSpecifier StepSound = new SoundCollectionSpecifier("FootstepBlood");
    [DataField] public TimeSpan ClearTime = TimeSpan.FromSeconds(5);
}

/// <summary>Нейтронный активатор: запускает камеры в радиусе 3 (nuclear_starter).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionStarterGrenadeComponent : Component
{
    [DataField] public float Range = 3;
}

/// <summary>Бассейн выдержки: стержни в нём не излучают, мобов тушит и отмывает.</summary>
[RegisterComponent]
public sealed partial class FissionPoolComponent : Component;

/// <summary>Выброшенный аварией стержень охладителя (immovablerod/nuclear_rod).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionEjectedRodComponent : Component
{
    public const string ContainerId = "fission_ejected";
}
