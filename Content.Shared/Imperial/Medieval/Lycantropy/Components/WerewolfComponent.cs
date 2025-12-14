using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WerewolfComponent : Component
{
    [AutoNetworkedField]
    public EntityUid? InfectAction;

    public bool InfectOn = false;

    public bool TearingOn = false;

    public Dictionary<EntityUid, TimeSpan> Critted = new();

    [AutoNetworkedField]
    public Dictionary<string, EntityUid> Actions = new();

    public TimeSpan? RevertTime;
    public TimeSpan? NextRevertPopup;
}
