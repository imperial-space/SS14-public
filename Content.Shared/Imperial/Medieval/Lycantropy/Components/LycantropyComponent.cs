using Content.Shared.Polymorph;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LycantropyComponent : Component
{
    [AutoNetworkedField]
    public int Points = 0;

    [AutoNetworkedField]
    public List<ProtoId<LycantropyAbilityPrototype>> Abilities = new();

    [AutoNetworkedField]
    public Dictionary<string, EntityUid> Actions = new();

    [AutoNetworkedField]
    public ProtoId<PolymorphPrototype>? SelectedForm = null;

    [DataField]
    public Dictionary<string, ProtoId<PolymorphPrototype>> AllowedPolymorphs = new()
    {
        { "werewolf-black", "WerwolfBlack" },
        { "werewolf-dark-blue", "WerwolfBlueDark" },
        { "werewolf-dark-light", "WerwolfBlueLight" },
        { "werewolf-brown", "WerwolfBrown" },
        { "werewolf-light", "WerwolfLight" },
        { "werewolf-purple", "WerwolfPurple" },
        { "werewolf-red", "WerwolfRed" },
    };

    [ViewVariables(VVAccess.ReadWrite)]
    public int NightsSpent = 0;
}
