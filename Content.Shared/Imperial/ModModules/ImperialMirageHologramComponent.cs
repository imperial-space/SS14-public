using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>Голограмма-мираж: копирует спрайт источника на клиенте.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialMirageHologramComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Source;

    [ViewVariables]
    public TimeSpan NextMove;
}
