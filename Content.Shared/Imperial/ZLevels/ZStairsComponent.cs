using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>
/// Лестница (/obj/structure/stairs): направление подъёма — куда она повёрнута. Подъём происходит на последней
/// ступени цепочки, когда уходишь с неё вперёд; спуск — шагом в открытое пространство над ней.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZStairsComponent : Component
{
    /// <summary>Есть ли у лестницы левый/средний/правый спрайты (stairs-l/m/r).</summary>
    [DataField]
    public bool MergedSprites = true;

    /// <summary>Базовое имя состояния спрайта.</summary>
    [DataField]
    public string BaseState = "stairs";

    /// <summary>Площадка наверху: метка на клетке над ступенью, шаг с которой назад спускает на лестницу (только сервер).</summary>
    [ViewVariables]
    public EntityUid? Top;
}
