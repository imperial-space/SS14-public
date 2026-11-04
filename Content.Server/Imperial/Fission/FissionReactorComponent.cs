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
