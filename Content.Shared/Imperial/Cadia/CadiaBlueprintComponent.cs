using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Голограмма чертежа в мире: силуэт будущей постройки. Сапёр подаёт материал и строит её на месте.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CadiaBlueprintComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaBlueprintKind Kind;

    /// <summary>Что построится (прототип) — по нему клиент рисует голографический силуэт.</summary>
    [DataField, AutoNetworkedField]
    public string Prototype = string.Empty;

    [DataField, AutoNetworkedField]
    public CadiaFaction Faction;

    [DataField, AutoNetworkedField]
    public int ElementId;

    [DataField, AutoNetworkedField]
    public int Cost;
}
