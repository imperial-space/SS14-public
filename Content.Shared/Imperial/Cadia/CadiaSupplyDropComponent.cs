using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Ящик спускается с неба на парашюте. Сервер считает время, клиент рисует спуск.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CadiaSupplyDropComponent : Component
{
    /// <summary>Что появится при посадке: по нему же клиент рисует груз под куполом.</summary>
    [DataField, AutoNetworkedField]
    public string Item = string.Empty;

    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public TimeSpan LandAt;
}
