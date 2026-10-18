using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Morph;

/// <summary>
/// mob/living/simple_animal/hostile/morph (Paradise, miniantags/morph/morph.dm).
/// Сетевые поля нужны клиенту: он копирует спрайт формы прямо на морфа.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class MorphComponent : Component
{
    // ─── Маскировка (spell/mimic/morph) ─────────────────────────────────

    /// <summary>morphed: морф сейчас в чужом облике.</summary>
    [DataField, AutoNetworkedField]
    public bool Morphed;

    /// <summary>Сущность, чей облик сейчас носит морф. Может быть уже удалена.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? FormSource;

    /// <summary>Прототип формы: запасной вариант, если исходная сущность пропала.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? FormProto;

    /// <summary>Растёт при каждой смене облика, чтобы клиент перерисовал спрайт.</summary>
    [DataField, AutoNetworkedField]
    public int FormVersion;

    /// <summary>perfect_disguise: при осмотре вблизи не выдаёт себя.</summary>
    [DataField, AutoNetworkedField]
    public bool PerfectDisguise;

    /// <summary>max_forms.</summary>
    [DataField]
    public int MaxForms = 5;

    /// <summary>available_forms: запомненные облики.</summary>
    [ViewVariables]
    public List<MorphForm> Forms = new();

    /// <summary>next_override_index: какой облик забыть следующим.</summary>
    [ViewVariables]
    public int NextOverrideIndex;

    /// <summary>initial(name) и initial(desc): что вернуть при restore_form().</summary>
    [ViewVariables]
    public string? OriginalName;

    [ViewVariables]
    public string? OriginalDescription;

    /// <summary>Приветствие и цели уже выданы этому разуму.</summary>
    [ViewVariables]
    public bool Greeted;

    // ─── Засада ─────────────────────────────────────────────────────────

    /// <summary>ambush_prepared.</summary>
    [DataField, AutoNetworkedField]
    public bool AmbushPrepared;

    [ViewVariables]
    public TimeSpan AmbushPreparedAt;

    /// <summary>MORPH_AMBUSH_PERFECTION_TIME.</summary>
    [DataField]
    public TimeSpan AmbushPerfectionTime = TimeSpan.FromSeconds(15);

    /// <summary>ambush_damage.</summary>
    [DataField]
    public float AmbushDamage = 25f;

    /// <summary>ambush_weaken.</summary>
    [DataField]
    public TimeSpan AmbushWeaken = TimeSpan.FromSeconds(6);

    // ─── Голод ──────────────────────────────────────────────────────────

    /// <summary>gathered_food: немного с самого начала, чтобы были способности.</summary>
    [DataField, AutoNetworkedField]
    public int GatheredFood = 20;

    /// <summary>ITEM_EAT_COST: несъедобное только отнимает еду.</summary>
    [DataField]
    public int ItemEatCost = 5;

    [DataField]
    public TimeSpan EatTime = TimeSpan.FromSeconds(3);

    // ─── Характеристики ─────────────────────────────────────────────────

    /// <summary>MORPHED_SPEED против speed = 1.5: в облике морф медленнее.</summary>
    [DataField]
    public float MorphedSpeedModifier = 0.6f;

    /// <summary>melee_damage 15 → 5 в облике.</summary>
    [DataField]
    public float MorphedDamageModifier = 1f / 3f;

    // ─── Способности ────────────────────────────────────────────────────

    [DataField]
    public EntProtoId MimicAction = "ActionMorphMimic";

    [DataField]
    public EntProtoId AmbushAction = "ActionMorphAmbush";

    [DataField]
    public EntProtoId ReproduceAction = "ActionMorphReproduce";

    [DataField]
    public EntProtoId OpenVentAction = "ActionMorphOpenVent";

    [DataField]
    public EntProtoId PassAirlockAction = "ActionMorphPassAirlock";

    /// <summary>Дополнительные способности (у магического морфа — дым и силовая стена).</summary>
    [DataField]
    public List<EntProtoId> ExtraActions = new();

    [ViewVariables]
    public List<EntityUid> GrantedActions = new();

    /// <summary>Что получает новый морф при размножении.</summary>
    [DataField]
    public EntProtoId ReproduceProto = "MobMorph";
}

/// <summary>datum/mimic_form: запомненный облик.</summary>
[DataDefinition, Serializable]
public sealed partial class MorphForm
{
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public EntityUid? Source;

    [DataField]
    public EntProtoId? Proto;
}

/// <summary>spell/morph_spell: цена способности в еде (hunger_cost).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MorphActionCostComponent : Component
{
    [DataField]
    public int HungerCost;
}

// ─── Способности ────────────────────────────────────────────────────────

/// <summary>spell/mimic/morph: по цели — запомнить облик, по себе — выбрать облик.</summary>
public sealed partial class MorphMimicActionEvent : EntityTargetActionEvent;

/// <summary>spell/morph_spell/ambush.</summary>
public sealed partial class MorphAmbushActionEvent : InstantActionEvent;

/// <summary>spell/morph_spell/reproduce.</summary>
public sealed partial class MorphReproduceActionEvent : InstantActionEvent;

/// <summary>spell/morph_spell/open_vent.</summary>
public sealed partial class MorphOpenVentActionEvent : InstantActionEvent;

/// <summary>spell/morph_spell/pass_airlock.</summary>
public sealed partial class MorphPassAirlockActionEvent : EntityTargetActionEvent;

// ─── Do-after ───────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed partial class MorphEatDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class MorphRememberFormDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class MorphTakeFormDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Index;

    private MorphTakeFormDoAfterEvent()
    {
    }

    public MorphTakeFormDoAfterEvent(int index)
    {
        Index = index;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class MorphAmbushDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class MorphPassAirlockDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class MorphOpenVentDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class MorphPryJawsDoAfterEvent : SimpleDoAfterEvent;

// ─── Окно выбора облика (tgui_input_list) ───────────────────────────────

[Serializable, NetSerializable]
public enum MorphFormUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class MorphFormBoundUserInterfaceState : BoundUserInterfaceState
{
    /// <summary>Имена запомненных обликов по порядку.</summary>
    public List<string> Forms { get; }

    /// <summary>Можно ли вернуться в истинный облик («Original Form»).</summary>
    public bool CanRestore { get; }

    public MorphFormBoundUserInterfaceState(List<string> forms, bool canRestore)
    {
        Forms = forms;
        CanRestore = canRestore;
    }
}

/// <summary>Выбор в окне: индекс облика или <see cref="OriginalForm"/>.</summary>
[Serializable, NetSerializable]
public sealed class MorphFormChosenMessage : BoundUserInterfaceMessage
{
    public const int OriginalForm = -1;

    public int Index { get; }

    public MorphFormChosenMessage(int index)
    {
        Index = index;
    }
}
