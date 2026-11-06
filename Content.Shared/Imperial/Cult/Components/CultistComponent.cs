using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// Культист Нар'Си (datum/antagonist/cult).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CultistComponent : Component
{
    /// <summary>Мастер культа (cult_leader_datum).</summary>
    [ViewVariables, AutoNetworkedField]
    public bool Leader;

    /// <summary>cult_eyes: культ «восстал».</summary>
    [ViewVariables, AutoNetworkedField]
    public bool RedEyes;

    /// <summary>cult_halo: культ «вознёсся».</summary>
    [ViewVariables, AutoNetworkedField]
    public bool Halo;

    /// <summary>Номер нимба halo1..halo6.</summary>
    [ViewVariables, AutoNetworkedField]
    public int HaloState = 1;

    /// <summary>Обращён руной (datum/antagonist/cult/converted).</summary>
    [ViewVariables]
    public bool Converted;

    /// <summary>Призрак-культист из «Царства духов».</summary>
    [ViewVariables]
    public bool CultGhost;

    /// <summary>Тихо выдать/снять роль (без приветствия).</summary>
    [ViewVariables]
    public bool Silent;

    [ViewVariables] public EntityUid? CommunionAction;
    [ViewVariables] public EntityUid? BloodMagicAction;
    [ViewVariables] public List<EntityUid> MasterActions = new();

    /// <summary>Подготовленные заклинания крови (действия).</summary>
    [ViewVariables] public List<EntityUid> Spells = new();

    /// <summary>Сейчас вырезает руны на коже.</summary>
    [ViewVariables] public bool ChannelingMagic;

    /// <summary>magic_enhanced (багровый медальон): 5 слотов и вдвое быстрее.</summary>
    [ViewVariables] public bool MagicEnhanced;

    /// <summary>Секунды метаболизма святой воды (deciseconds_metabolized).</summary>
    [ViewVariables] public TimeSpan HolyWaterSeconds;

    [ViewVariables] public TimeSpan NextHolyWaterMessage;

    [DataField]
    public TimeSpan SpellActionEmpDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan HorrorDuration = TimeSpan.FromSeconds(240);

    [DataField]
    public TimeSpan HorrorDelay = TimeSpan.FromMinutes(4);

    [DataField]
    public TimeSpan ConstructionDoAfterDuration = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan BeamDoAfterDelay = TimeSpan.FromSeconds(9);

    [DataField]
    public TimeSpan ReckoningDoAfterDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public float SpellActionEmpRange = 5f;

    [DataField]
    public float SpellActionEmpEnergyConsumption = 50000f;

    [DataField]
    public TimeSpan ShackleDoAfterSeconds = TimeSpan.FromSeconds(10f);

    [DataField]
    public float ConstructionDoAfterRange = 0.5f;

    [DataField]
    public float ConstructionDoAfterEnergyConsumption = 100000f;

    [DataField]
    public float BloodBondBaseThrowSpeed = 10f;

    [DataField]
    public TimeSpan BeamDoAfterSeconds = TimeSpan.FromSeconds(4f);

    [DataField]
    public TimeSpan EldritchPulseSeconds = TimeSpan.FromSeconds(0.4f);
}
