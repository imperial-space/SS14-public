using Content.Shared.Atmos;
using Content.Shared.Imperial.Hypertorus;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Hypertorus;

/// <summary>Роль детали гиперторуса.</summary>
public enum HypertorusPartType : byte
{
    FuelInput,
    ModeratorInput,
    WasteOutput,
    Interface,
    Corner,
}

/// <summary>
/// Ядро термоядерного реактора «Гиперторус» (hfr_core.dm). Держит две внутренние смеси — топливо (5000 л)
/// и модератор (10 000 л); охладитель — газ в трубе ядра на втором слое.
/// </summary>
[RegisterComponent]
public sealed partial class HypertorusCoreComponent : Component
{
    public const string PipeNode = "pipe";

    #region Ручки и переключатели

    [ViewVariables] public bool StartPower;
    [ViewVariables] public bool StartCooling;
    [ViewVariables] public bool StartFuel;
    [ViewVariables] public bool StartModerator;
    [ViewVariables] public bool WasteRemove;

    [ViewVariables] public float HeatingConductor = 100;
    [ViewVariables] public float MagneticConstrictor = 100;
    [ViewVariables] public float CurrentDamper;
    [ViewVariables] public float FuelInjectionRate = 25;
    [ViewVariables] public float ModeratorInjectionRate = 25;

    /// <summary>Объём охлаждения ядра (airs[1].volume), литры.</summary>
    [ViewVariables] public float CoolingVolume = 100;

    /// <summary>moderator_scrubbing: газы, которые фильтр забирает из модератора.</summary>
    [ViewVariables] public HashSet<Gas> ModeratorScrubbing = HypertorusFuelPrototype.ResolveGas("Helium") is { } helium ? new() { helium } : new();

    [ViewVariables] public float ModeratorFilteringRate = 100;

    [ViewVariables] public ProtoId<HypertorusFuelPrototype>? SelectedFuel;

    #endregion

    #region Состояние синтеза

    [ViewVariables] public GasMixture InternalFusion = new(5000) { Temperature = Atmospherics.T20C };
    [ViewVariables] public GasMixture ModeratorInternal = new(10000) { Temperature = Atmospherics.T20C };

    /// <summary>
    /// Температуры смесей ядра. Газовые смеси SS14 ограничены Atmospherics.Tmax (262 144 K),
    /// а синтез идёт до 10⁸ K — поэтому температура хранится отдельно, смеси держат только моли.
    /// </summary>
    [ViewVariables] public double FusionTemp = Atmospherics.T20C;
    [ViewVariables] public double ModeratorTemp = Atmospherics.T20C;

    [ViewVariables] public double Energy;
    [ViewVariables] public double CoreTemperature = Atmospherics.T20C;
    [ViewVariables] public double InternalPower;
    [ViewVariables] public double PowerOutput;
    [ViewVariables] public double Instability;
    [ViewVariables] public double DeltaTemperature;
    [ViewVariables] public double Conduction;
    [ViewVariables] public double Radiation;
    [ViewVariables] public double Efficiency;
    [ViewVariables] public double HeatLimiterModifier;
    [ViewVariables] public double HeatOutputMax;
    [ViewVariables] public double HeatOutputMin;
    [ViewVariables] public double HeatOutput;

    [ViewVariables] public int PowerLevel;
    [ViewVariables] public float IronContent;

    #endregion

    #region Целостность и тревоги

    /// <summary>critical_threshold_proximity: 900 — расплавление.</summary>
    [ViewVariables] public float CriticalThresholdProximity;
    [ViewVariables] public float CriticalThresholdProximityArchived;

    [DataField] public float WarningPoint = 50;
    [DataField] public float EmergencyPoint = 700;
    [DataField] public float MeltingPoint = 900;

    [ViewVariables] public bool HasReachedEmergency;
    [ViewVariables] public TimeSpan LastWarning = TimeSpan.FromSeconds(-1000);
    [ViewVariables] public HypertorusDamageFlags WarningDamageFlags;

    [DataField] public ProtoId<RadioChannelPrototype> EngineeringChannel = "Engineering";
    [DataField] public ProtoId<RadioChannelPrototype> CommonChannel = "Common";

    /// <summary>Идёт обратный отсчёт до расплавления.</summary>
    [ViewVariables] public bool FinalCountdown;
    [ViewVariables] public int CountdownTenths;
    [ViewVariables] public TimeSpan NextCountdownStep;

    #endregion

    #region Связанные детали

    [ViewVariables] public bool Active;
    [ViewVariables] public bool FusionStarted;
    [ViewVariables] public EntityUid? Interface;
    [ViewVariables] public EntityUid? FuelInput;
    [ViewVariables] public EntityUid? ModeratorInput;
    [ViewVariables] public EntityUid? WasteOutput;
    [ViewVariables] public List<EntityUid> Corners = new();

    #endregion

    #region Температуры для интерфейса

    [ViewVariables] public float FusionTemperature;
    [ViewVariables] public float FusionTemperatureArchived;
    [ViewVariables] public float ModeratorTemperature;
    [ViewVariables] public float ModeratorTemperatureArchived;
    [ViewVariables] public float CoolantTemperature;
    [ViewVariables] public float CoolantTemperatureArchived;
    [ViewVariables] public float OutputTemperature;
    [ViewVariables] public float OutputTemperatureArchived;
    [ViewVariables] public float TemperaturePeriod = 1;

    #endregion

    #region Звук

    [DataField]
    public SoundSpecifier CalmSound = new SoundCollectionSpecifier("HypertorusCalm");

    [DataField]
    public SoundSpecifier MeltingSound = new SoundCollectionSpecifier("HypertorusMelting");

    [DataField]
    public SoundSpecifier CriticalExplosionSound = new SoundPathSpecifier("/Audio/Imperial/Hypertorus/HFR_critical_explosion.ogg");

    [DataField]
    public SoundSpecifier MeltingAlarm = new SoundPathSpecifier("/Audio/Announcements/bloblarm.ogg");

    [DataField]
    public SoundSpecifier EmergencyAlarm = new SoundPathSpecifier("/Audio/Imperial/Hypertorus/engine_alert1.ogg");

    [DataField]
    public SoundSpecifier DangerAlarm = new SoundPathSpecifier("/Audio/Imperial/Hypertorus/engine_alert2.ogg");

    [DataField]
    public SoundSpecifier WarningAlarm = new SoundPathSpecifier("/Audio/Imperial/Hypertorus/terminal_alert.ogg");

    [DataField]
    public SoundSpecifier ZapSound = new SoundPathSpecifier("/Audio/Imperial/Hypertorus/emitter2.ogg");

    [ViewVariables] public TimeSpan LastAccentSound;

    #endregion

    /// <summary>Накопленное время атмос-тиков: процесс идёт шагами по 0.5 с, как SSair в SS13.</summary>
    [ViewVariables] public float Accumulator;

    /// <summary>Сущность-источник радиации на время аварийного импульса.</summary>
    [DataField]
    public EntProtoId RadiationPulse = "ImperialHypertorusRadiationPulse";

    [DataField]
    public EntProtoId NuclearParticle = "ImperialHypertorusParticle";
}

/// <summary>warning_damage_flags: что именно разрушает гиперторус.</summary>
[Flags]
public enum HypertorusDamageFlags
{
    None = 0,
    HighPowerDamage = 1 << 0,
    HighFuelMixMole = 1 << 1,
    IronContentDamage = 1 << 2,
    IronContentIncrease = 1 << 3,
    Emped = 1 << 4,
}

/// <summary>Деталь гиперторуса вокруг ядра: порт, интерфейс или угол.</summary>
[RegisterComponent]
public sealed partial class HypertorusPartComponent : Component
{
    [DataField(required: true)]
    public HypertorusPartType PartType;

    /// <summary>Коробка, в которую деталь разбирается ломом.</summary>
    [DataField(required: true)]
    public EntProtoId Box;

    [ViewVariables] public bool Active;
    [ViewVariables] public bool FusionStarted;
    [ViewVariables] public bool PanelOpen;
    [ViewVariables] public bool Cracked;
    [ViewVariables] public EntityUid? Core;

    /// <summary>Интерфейс выдаёт памятку при первой активации.</summary>
    [ViewVariables] public bool Activated;
}

/// <summary>Панель ядра (screwdriver_act у core).</summary>
[RegisterComponent]
public sealed partial class HypertorusCorePanelComponent : Component
{
    [ViewVariables] public bool PanelOpen;

    [DataField]
    public EntProtoId Box = "ImperialHypertorusBoxCore";
}

public enum HypertorusBoxType : byte
{
    Corner,
    Body,
    Core,
}

/// <summary>Коробка детали (obj/item/hfr_box): ставится 3×3, ядро разворачивает всё мультитулом.</summary>
[RegisterComponent]
public sealed partial class HypertorusBoxComponent : Component
{
    [DataField(required: true)]
    public HypertorusBoxType BoxType;

    [DataField(required: true)]
    public EntProtoId Part;
}
