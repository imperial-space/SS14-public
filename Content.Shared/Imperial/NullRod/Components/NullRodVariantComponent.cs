using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Форма святого оружия, в которую можно превратить нулевой стержень.
/// Все прототипы с этим компонентом и <see cref="ChaplainSpawnable"/> попадают в радиальное меню стержня.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NullRodVariantComponent : Component
{
    /// <summary>Краткое описание возможностей формы для радиального меню.</summary>
    [DataField(required: true)]
    public LocId MenuDescription;

    /// <summary>Можно ли выбрать эту форму в меню стержня.</summary>
    [DataField]
    public bool ChaplainSpawnable = true;

    /// <summary>Что ещё появляется при выборе формы (колчан святых стрел у божественного лука).</summary>
    [DataField]
    public List<EntProtoId> SpawnOnPick = new();
}
