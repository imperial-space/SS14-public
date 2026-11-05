using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.StationAi;

/// <summary>mimic/copy/machine: оживлённая машина рисуется спрайтом своего прототипа.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialMalfMimicComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? Proto;
}
