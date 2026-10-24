using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Очки ночного видения TG (glasses/night): выдают носителю ImperialNightVision.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialGrantNightVisionComponent : Component
{
    [ViewVariables]
    public bool Granted;
}

/// <summary>Пневматический затвор шлюза (door_seal): дверь нельзя открыть, пока он стоит.</summary>
[RegisterComponent]
public sealed partial class ImperialPneumaticSealComponent : Component
{
    [DataField]
    public float SealTime = 2f;

    [DataField]
    public float UnsealTime = 4f;
}

/// <summary>Дверь, заблокированная пневматическим затвором.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialSealedDoorComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Seal = "ImperialPneumaticSeal";
}

[Serializable, NetSerializable]
public sealed partial class ImperialSealDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ImperialUnsealDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Болтерный ключ (wrench/bolter): поднимает и опускает болты шлюза без питания.</summary>
[RegisterComponent]
public sealed partial class ImperialBolterWrenchComponent : Component
{
    [DataField]
    public float Delay = 3f;
}

[Serializable, NetSerializable]
public sealed partial class ImperialBolterWrenchDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Дальнобойный газоанализатор (analyzer/ranged): анализ тайла на расстоянии.</summary>
[RegisterComponent]
public sealed partial class ImperialRangedGasAnalyzerComponent : Component
{
    [DataField]
    public float Range = 15f;
}

/// <summary>Датчик здоровья (assembly/health): срабатывает, когда носитель впадает в крит или умирает.</summary>
[RegisterComponent]
public sealed partial class ImperialHealthSensorComponent : Component
{
    /// <summary>true — ждать смерти, false — критического состояния.</summary>
    [DataField]
    public bool WaitForDeath;

    [DataField]
    public bool Scanning = true;

    [ViewVariables]
    public bool Triggered;
}

/// <summary>Индуктор TG: заряжает батареи и ячейки других устройств от своей ячейки.</summary>
[RegisterComponent]
public sealed partial class ImperialInducerComponent : Component
{
    /// <summary>Сколько энергии передаётся за одну итерацию (power_transfer).</summary>
    [DataField]
    public float Transfer = 200f;

    [DataField]
    public float Delay = 1f;
}

[Serializable, NetSerializable]
public sealed partial class ImperialInducerDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Портативная воздушная помпа (portable_atmospherics/pump).</summary>
[RegisterComponent]
public sealed partial class ImperialPortablePumpComponent : Component
{
    [DataField]
    public GasMixture Air = new(1000f) { Temperature = Atmospherics.T20C };

    /// <summary>true — качать из бака наружу, false — с тайла в бак.</summary>
    [DataField]
    public bool PumpOut;

    [DataField]
    public bool Enabled;

    [DataField]
    public float TargetPressure = Atmospherics.OneAtmosphere;

    [DataField]
    public float MaxPressure = Atmospherics.OneAtmosphere * 25;

    /// <summary>Скорость, литров за тик атмосферы.</summary>
    [DataField]
    public float TransferRate = 200f;
}

/// <summary>
/// Генератор атмосферного щита (atmos_shield_gen): два генератора лицом друг к другу
/// на расстоянии до maxRange тайлов держат между собой газонепроницаемое поле.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialAtmosShieldGenComponent : Component
{
    [DataField]
    public int MaxRange = 2;

    [DataField]
    public EntProtoId Field = "ImperialAtmosShieldField";

    [ViewVariables]
    public List<EntityUid> Fields = new();

    [ViewVariables]
    public TimeSpan NextCheck;
}

/// <summary>Особые лучи дисков эмиттера TG.</summary>
[RegisterComponent]
public sealed partial class ImperialEmitterBeamEffectsComponent : Component
{
    /// <summary>adjust_fire_stacks у зажигательного луча.</summary>
    [DataField]
    public float FireStacks;

    /// <summary>Рассудок цели (психосифонный луч).</summary>
    [DataField]
    public float Sanity;

    /// <summary>Радиус магнитного вихря (goonchem_vortex).</summary>
    [DataField]
    public float VortexRange;
}

/// <summary>Т-лучевые очки: сканер работает только надетым на глаза.</summary>
[RegisterComponent]
public sealed partial class ImperialWornTrayScannerComponent : Component
{
    [DataField]
    public float Range = 4f;
}
