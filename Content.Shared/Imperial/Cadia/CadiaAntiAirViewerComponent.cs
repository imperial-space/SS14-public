using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Наводчик смотрит на борт через камеру зенитки: курсор — прицел, существа на борту скрыты.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaAntiAirViewerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Console;

    [DataField, AutoNetworkedField]
    public EntityUid? Gun;

    [DataField, AutoNetworkedField]
    public EntityUid? Ship;

    [ViewVariables]
    public bool HadFov;
}
