using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// Помечает игрока последователем Нар'Си.
/// Добавляется при конвертации или спавне начального последователя.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CultistComponent : Component
{
    public override bool SessionSpecific => true;
    /// <summary>
    /// Иконка фракции, отображаемая над головой культиста.
    /// </summary>
    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon = "CultistFaction";

    /// <summary>
    /// Находится ли культист на руне усиления (снижает стоимость заклинаний).
    /// </summary>
    [AutoNetworkedField]
    public bool OnEmpowerRune;

    /// <summary>
    /// Отображать ли красные глаза (вуаль ослаблена).
    /// </summary>
    [AutoNetworkedField]
    public bool RedEyes;

    /// <summary>
    /// Отображать ли кровавый нимб (культ раскрыт).
    /// </summary>
    [AutoNetworkedField]
    public bool BloodHalo;

    /// <summary>
    /// Оригинальный цвет глаз до включения красных глаз.
    /// </summary>
    [DataField]
    public Color? OriginalEyeColor;

    /// <summary>
    /// Entity actions, выданные этому культисту при вступлении (для удаления при деконверсии).
    /// </summary>
    public List<EntityUid> GrantedActions = new();

    /// <summary>
    /// Number of spells currently prepared via Blood Magic.
    /// </summary>
    public int ActiveSpellCount;

    /// <summary>
    /// IDs of spells granted via Blood Magic (for tracking).
    /// </summary>
    public List<string> PreparedSpells = new();

    /// <summary>
    /// Remaining uses for each prepared blood spell action.
    /// Key is the prepared action prototype ID.
    /// </summary>
    public Dictionary<string, int> PreparedSpellUses = new();

    /// <summary>
    /// Заряды Кровавого обряда (отдельная валюта, накапливается сбором крови).
    /// </summary>
    [AutoNetworkedField]
    public int BloodRitesCharges;

    /// <summary>
    /// Entity-держатель BUI для Blood Magic и Commune (не требует кинжала).
    /// Создаётся при AddCultist, удаляется при RemoveCultist.
    /// </summary>
    public EntityUid? BuiHolder;

    /// <summary>
    /// Следующая серверная проверка holy/unholy water в bloodstream.
    /// </summary>
    public TimeSpan NextReagentCheck;

    /// <summary>
    /// Когда на культисте впервые накопилось достаточно святой воды для деконверта.
    /// </summary>
    public TimeSpan? HolyWaterThresholdReachedAt;

    /// <summary>
    /// Зацикленная музыка ритуала Нар'Си, пока идёт начертание.
    /// </summary>
    public EntityUid? ActiveNarSieRitualAudio;
}