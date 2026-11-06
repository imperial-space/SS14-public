using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.FixedPoint;

namespace Content.Shared.Imperial.Changeling;

/// <summary>/datum/antagonist/changeling: механика генокрада на теле.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialChangelingComponent : Component
{
    /// <summary>chem_charges.</summary>
    [DataField, AutoNetworkedField]
    public float Chemicals = 20;

    /// <summary>total_chem_storage.</summary>
    [DataField, AutoNetworkedField]
    public float MaxChemicals = 75;

    /// <summary>chem_recharge_rate: химикатов в секунду.</summary>
    [DataField]
    public float RechargeRate = 1;

    /// <summary>chem_recharge_slowdown.</summary>
    [DataField]
    public float RechargeSlowdown;

    /// <summary>genetic_points.</summary>
    [DataField, AutoNetworkedField]
    public int GeneticPoints = 10;

    /// <summary>total_genetic_points.</summary>
    [DataField, AutoNetworkedField]
    public int TotalGeneticPoints = 10;

    /// <summary>absorbed_count: ДНК, включая жало извлечения.</summary>
    [DataField, AutoNetworkedField]
    public int AbsorbedCount;

    /// <summary>true_absorbs: только настоящие поглощения.</summary>
    [DataField, AutoNetworkedField]
    public int TrueAbsorbs;

    /// <summary>can_respec.</summary>
    [DataField, AutoNetworkedField]
    public int CanRespec;

    /// <summary>dna_max.</summary>
    [DataField]
    public int DnaMax = 6;

    /// <summary>sting_range.</summary>
    [DataField]
    public float StingRange = 2;

    /// <summary>changelingID: позывной в улье.</summary>
    [DataField, AutoNetworkedField]
    public string ChangelingId = "Changeling";

    /// <summary>hive_name.</summary>
    [DataField, AutoNetworkedField]
    public string HiveName = string.Empty;

    /// <summary>purchased_powers.</summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<ImperialChangelingPowerPrototype>> Purchased = new();

    /// <summary>innate_powers.</summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<ImperialChangelingPowerPrototype>> Innate = new();

    /// <summary>Действия способностей (врождённых и купленных).</summary>
    [ViewVariables]
    public Dictionary<ProtoId<ImperialChangelingPowerPrototype>, EntityUid> Actions = new();

    [ViewVariables]
    public EntityUid? EmporiumAction;

    /// <summary>/datum/action/antag_info.</summary>
    [ViewVariables]
    public EntityUid? InfoAction;

    /// <summary>chosen_sting.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ImperialChangelingPowerPrototype>? ChosenSting;

    /// <summary>lesserform/select_form(): радиалка выбирает облик для возвращения из низшей формы.</summary>
    [DataField, AutoNetworkedField]
    public bool SelectingHumanForm;

    /// <summary>transformation/set_sting(): радиалка выбирает ДНК для жала, а не облик.</summary>
    [DataField, AutoNetworkedField]
    public bool SelectingStingDna;

    /// <summary>transformation/selected_dna.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? StingDna;

    /// <summary>stored_profiles по порядку: копии жертв на паузной карте (ванильное хранилище личностей).</summary>
    [DataField, AutoNetworkedField]
    public List<EntityUid> Profiles = new();

    /// <summary>first_profile: исходный облик, защищён от вытеснения.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? FirstProfile;

    /// <summary>current_profile.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? CurrentProfile;

    /// <summary>stolen_memories: имя жертвы → воспоминание.</summary>
    [ViewVariables]
    public List<ImperialChangelingMemory> Memories = new();

    /// <summary>antag_memory: цели и тайны поглощённых.</summary>
    [ViewVariables]
    public string StolenAntagInfo = string.Empty;

    /// <summary>Сейчас идёт поглощение (is_absorbing).</summary>
    [ViewVariables]
    public bool Absorbing;

    /// <summary>TRAIT_DEATHCOMA от стазиса.</summary>
    [DataField, AutoNetworkedField]
    public bool FakeDeath;

    /// <summary>revive_ready.</summary>
    [DataField, AutoNetworkedField]
    public bool ReviveReady;

    [ViewVariables]
    public TimeSpan? ReviveReadyAt;

    /// <summary>Включённые переключаемые способности (active).</summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> Toggled = new();

    /// <summary>/datum/status_effect/fleshmend.</summary>
    [ViewVariables]
    public TimeSpan? FleshmendUntil;

    /// <summary>strained_muscles/stacks.</summary>
    [ViewVariables]
    public int MuscleStacks;

    [ViewVariables]
    public TimeSpan NextMuscleTick;

    /// <summary>Стрелка феромонов: pinonclose/medium/far/direct/null.</summary>
    [DataField, AutoNetworkedField]
    public string? PheromoneState;

    [DataField, AutoNetworkedField]
    public Direction PheromoneDir;

    [ViewVariables]
    public TimeSpan NextPheromoneTick;

    /// <summary>override_voice.</summary>
    [ViewVariables]
    public string? MimicVoice;

    /// <summary>void_adaption/currently_active.</summary>
    [ViewVariables]
    public bool VoidActive;

    /// <summary>Генокрад-червь (headslug): слабее обычного, без целей.</summary>
    [DataField]
    public bool Headslug;

    public override bool SendOnlyToOwner => true;

    [DataField]
    public TimeSpan UseLastResortDelay = TimeSpan.FromSeconds(1.1);

    [DataField]
    public TimeSpan MuteActionTime = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan BlindActionTime = TimeSpan.FromSeconds(40);

    [DataField]
    public TimeSpan MuscleTickDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan UpdateUtilityDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan MuscleTickDelay2 = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan PheromoneTickDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ShowFleshmendAlertDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan FleshmendDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan UseBiodegradeDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan PunishWithAcidDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan UseResonantShriekDuration = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan UseResonantShriekBoozePowerDelay = TimeSpan.FromSeconds(25);

    [DataField]
    public TimeSpan UseResonantShriekTime = TimeSpan.FromSeconds(100);

    [DataField]
    public TimeSpan UseTechnoShriekDuration = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan SetMusclesDuration = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan DefibZapDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan DefibZapDuration2 = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan DefibZapTime = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan DefibZapBoozePowerDelay = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan DefibZapTime2 = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan FinishDefibGraspTime = TimeSpan.FromSeconds(20);

    [DataField]
    public float FinishAbsorbAmount = 10f;

    [DataField]
    public FixedPoint2 FinishAbsorbAmount2 = -1000;

    [DataField]
    public float UseRegenerateAmount = -1000f;

    [DataField]
    public FixedPoint2 UseRegenerateAmount2 = 1000;

    [DataField]
    public FixedPoint2 CryoActionQuantity = 30;

    [DataField]
    public FixedPoint2 UseAdrenalineQuantity = 4;

    [DataField]
    public FixedPoint2 UsePanaceaQuantity = 10;

    [DataField]
    public FixedPoint2 UsePanaceaQuantity2 = 20;

    [DataField]
    public FixedPoint2 UsePanaceaQuantity3 = 25;

    [DataField]
    public float UseResonantShriekRange = 4f;

    [DataField]
    public float UseResonantShriekRange2 = 4.5f;

    [DataField]
    public float UseTechnoShriekRange = 5f;

    [DataField]
    public float UseTechnoShriekEnergyConsumption = 50000f;
}
