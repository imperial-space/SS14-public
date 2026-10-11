using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.NinjaHeadset.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaHeadsetComponent : Component
{
    [DataField("copyFrequenciesTime"), AutoNetworkedField]
    public TimeSpan CopyFrequenciesTime = TimeSpan.FromSeconds(5);

    [DataField("copiedFrequencies"), AutoNetworkedField]
    public List<ProtoId<RadioChannelPrototype>> CopiedFrequencies = new();
}
