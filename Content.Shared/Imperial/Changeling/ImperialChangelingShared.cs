using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>category способности в магазине эволюции.</summary>
public enum ImperialChangelingCategory : byte
{
    Stings,
    Combat,
    Stealth,
    Utility,
}

/// <summary>req_stat: наихудшее состояние, в котором способность ещё работает (CONSCIOUS, HARD_CRIT, DEAD).</summary>
public enum ImperialChangelingStat : byte
{
    Conscious,
    HardCrit,
    Dead,
}

/// <summary>/datum/action/changeling: способность генокрада.</summary>
[Prototype("imperialChangelingPower")]
public sealed partial class ImperialChangelingPowerPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    /// <summary>helptext: мелкий шрифт в магазине.</summary>
    [DataField]
    public LocId? HelpText;

    /// <summary>button_icon_state в Imperial/Changeling/actions.rsi.</summary>
    [DataField]
    public string Icon = "changelingsting";

    [DataField]
    public ImperialChangelingCategory Category = ImperialChangelingCategory.Utility;

    /// <summary>dna_cost: очки генов; -1 — CHANGELING_POWER_UNOBTAINABLE.</summary>
    [DataField]
    public int DnaCost = -1;

    /// <summary>CHANGELING_POWER_INNATE: выдаётся сразу.</summary>
    [DataField]
    public bool Innate;

    /// <summary>chemical_cost.</summary>
    [DataField]
    public int ChemicalCost;

    [DataField]
    public int ReqDna;

    [DataField]
    public int ReqAbsorbs;

    /// <summary>req_human: недоступна в низшей форме.</summary>
    [DataField]
    public bool ReqHuman;

    [DataField]
    public ImperialChangelingStat ReqStat = ImperialChangelingStat.Conscious;

    /// <summary>ignores_fakedeath.</summary>
    [DataField]
    public bool IgnoresFakedeath;

    /// <summary>disabled_by_fire.</summary>
    [DataField]
    public bool DisabledByFire = true;

    /// <summary>Действие, которое получает генокрад.</summary>
    [DataField]
    public EntProtoId? Action;
}

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
}

/// <summary>Жертва, высушенная генокрадом (Drain): её нельзя поглотить снова.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialChangelingAbsorbedComponent : Component;

/// <summary>Нажатие кнопки способности без цели.</summary>
public sealed partial class ImperialChangelingPowerActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public ProtoId<ImperialChangelingPowerPrototype> Power;
}

/// <summary>Способность с целью-существом.</summary>
public sealed partial class ImperialChangelingTargetPowerActionEvent : EntityTargetActionEvent
{
    [DataField(required: true)]
    public ProtoId<ImperialChangelingPowerPrototype> Power;
}

/// <summary>/datum/action/cellular_emporium.</summary>
public sealed partial class ImperialChangelingEmporiumActionEvent : InstantActionEvent;

/// <summary>Одна итерация поглощения (attempt_absorb, 3 × 15 с).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialChangelingAbsorbDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Stage;

    public ImperialChangelingAbsorbDoAfterEvent(int stage)
    {
        Stage = stage;
    }

    public override DoAfterEvent Clone() => new ImperialChangelingAbsorbDoAfterEvent(Stage);
}

[Serializable, NetSerializable]
public enum ImperialChangelingUiKey : byte
{
    Emporium,
    Transform,
    Info,
    Choice,
}

[Serializable, NetSerializable]
public sealed class ImperialChangelingEvolveMessage(string power) : BoundUserInterfaceMessage
{
    public readonly string Power = power;
}

[Serializable, NetSerializable]
public sealed class ImperialChangelingReadaptMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ImperialChangelingTransformSelectMessage(NetEntity profile) : BoundUserInterfaceMessage
{
    public readonly NetEntity Profile = profile;
}

/// <summary>«Сбросить маскировку из плоти».</summary>
[Serializable, NetSerializable]
public sealed class ImperialChangelingDropFleshMessage : BoundUserInterfaceMessage;

/// <summary>Жертва трансформирующего жала: через 8 минут (пока жива) вернётся в свой облик.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingStungTransformComponent : Component
{
    /// <summary>Копия исходного облика на паузной карте.</summary>
    [ViewVariables]
    public EntityUid? Original;

    /// <summary>Осталось; INFINITY для низших форм.</summary>
    [ViewVariables]
    public TimeSpan? Remaining;
}

/// <summary>/obj/item/melee/arm_blade: скрежет airlock_alien_prying при вскрытии запитанного шлюза.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingArmBladeComponent : Component
{
    [DataField]
    public SoundSpecifier PrySound = new SoundPathSpecifier("/Audio/Imperial/Changeling/airlock_alien_prying.ogg");
}

/// <summary>/obj/item/melee/arm_blade/false: исчезает через минуту.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingFalseBladeComponent : Component
{
    [ViewVariables]
    public TimeSpan RemoveAt;

    [ViewVariables]
    public EntityUid? Holder;
}

/// <summary>/obj/item/shield/changeling: блокирует половину ударов, пока не кончатся remaining_uses.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingShieldComponent : Component
{
    [DataField]
    public float BlockChance = 0.5f;

    [ViewVariables]
    public int RemainingUses;
}

/// <summary>/obj/item/gun/magic/tentacle.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingTentacleComponent : Component;

/// <summary>/obj/projectile/tentacle.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingTentacleProjectileComponent : Component;

/// <summary>/obj/item/clothing/head/helmet/changeling_hivehead.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingHiveHeadComponent : Component
{
    /// <summary>spawn_count.</summary>
    [DataField]
    public int SpawnCount = 6;

    /// <summary>/mob/living/basic/bee/timed/short: lifespan.</summary>
    [DataField]
    public TimeSpan Lifespan = TimeSpan.FromSeconds(25);
}

/// <summary>headcrab: animate(user, transform = user.transform * 1.5, color = COLOR_RED, time = 1 SECONDS).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialChangelingSwellComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan StartTime;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1);

    [DataField]
    public float Scale = 1.5f;
}

/// <summary>/datum/action/cooldown/hivehead_spawn_minions.</summary>
public sealed partial class ImperialChangelingReleaseBeesEvent : InstantActionEvent;

/// <summary>ismonkey(): генокрад в низшей форме.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialChangelingLesserFormComponent : Component;

/// <summary>/mob/living/basic/headslug.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingHeadslugComponent : Component
{
    /// <summary>egg_lain.</summary>
    [DataField]
    public bool EggLain;

    /// <summary>addtimer(death, 10 SECONDS) после кладки.</summary>
    [ViewVariables]
    public TimeSpan? DieAt;
}

/// <summary>/obj/item/organ/body_egg/changeling_egg в мёртвом теле.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingEggComponent : Component
{
    /// <summary>origin: разум генокрада.</summary>
    [ViewVariables]
    public EntityUid? Origin;

    /// <summary>hatch_time: EGG_INCUBATION_TIME = 4 минуты.</summary>
    [ViewVariables]
    public TimeSpan HatchAt;
}

/// <summary>/datum/action/antag_info: кнопка досье генокрада.</summary>
public sealed partial class ImperialChangelingInfoActionEvent : InstantActionEvent;

/// <summary>/atom/movable/screen/ling/sting/Click(): убрать жало щелчком по иконке.</summary>
[Serializable, NetSerializable]
public sealed class ImperialChangelingUnsetStingEvent : EntityEventArgs;

/// <summary>Украденное воспоминание (stolen_memories: name → story).</summary>
[Serializable, NetSerializable]
public sealed class ImperialChangelingMemory(string name, string story)
{
    public readonly string Name = name;
    public readonly string Story = story;
}

/// <summary>ui_data() AntagInfoChangeling.</summary>
[Serializable, NetSerializable]
public sealed class ImperialChangelingInfoState(
    string trueName,
    string hiveName,
    int absorbedDna,
    List<string> objectives,
    List<ImperialChangelingMemory> memories,
    string stolenAntagInfo) : BoundUserInterfaceState
{
    public readonly string TrueName = trueName;
    public readonly string HiveName = hiveName;
    public readonly int AbsorbedDna = absorbedDna;
    public readonly List<string> Objectives = objectives;
    public readonly List<ImperialChangelingMemory> Memories = memories;
    public readonly string StolenAntagInfo = stolenAntagInfo;
}

/// <summary>tgui_alert / tgui_input_list: вопрос с вариантами ответа.</summary>
[Serializable, NetSerializable]
public sealed class ImperialChangelingChoiceState(string title, string message, List<string> options) : BoundUserInterfaceState
{
    public readonly string Title = title;
    public readonly string Message = message;
    public readonly List<string> Options = options;
}

[Serializable, NetSerializable]
public sealed class ImperialChangelingChoiceMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}
