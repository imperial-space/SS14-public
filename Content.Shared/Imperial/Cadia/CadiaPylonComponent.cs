using Content.Shared.Dataset;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Кадийский пилон — древний обелиск, сдерживающий Око Ужаса. Пока пилоны стоят, варп слаб:
/// демоны ослаблены, призывы дороже. Падение всех пилонов — победа Хаоса.
/// До начала штурма пилоны неуязвимы (их защищает поле).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaPylonComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Shielded = true;

    /// <summary>Номер пилона для объявлений («Пилон III пал»).</summary>
    [DataField, AutoNetworkedField]
    public int Number;
}
