using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>
/// Z-уровень как в SS13: вся карта — один этаж. Карты с одинаковым <see cref="Stack"/> складываются в стопку по
/// <see cref="Level"/> (0 — нижний). Этажи совмещены по мировым координатам: клетка над клеткой — та же точка мира.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZLevelMapComponent : Component
{
    /// <summary>Имя стопки этажей. Пустое — карта ни с чем не связана.</summary>
    [DataField, AutoNetworkedField]
    public string Stack = string.Empty;

    [DataField, AutoNetworkedField]
    public int Level;

    /// <summary>Карта этажом ниже (выставляется системой при связывании стопки).</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Below;

    /// <summary>Карта этажом выше.</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Above;

    /// <summary>
    /// На сколько этажей ниже лежит <see cref="Below"/>. Между ними может быть пустой воздух (небо Кадии — 7 этажей
    /// над поверхностью): нижний этаж рисуется дальше, падение сквозь разрыв считается за все этажи.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int BelowGap = 1;

    /// <summary>Пол этого этажа не обрушается без опоры (поле боя Кадии: небо, орбита, крейсеры).</summary>
    [DataField]
    public bool NoCollapse;
}
