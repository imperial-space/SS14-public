using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.ChargeableAnnounce;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ChargeableAnnounceComponent : Component
{
    [DataField("ischarged"), AutoNetworkedField]
    public bool IsCharged = true;

    [DataField("chargedstate")]
    public string ChargedState = string.Empty;

    [DataField("unchargedstate")]
    public string UnchargedState = "inactivecomm";

    [DataField("rechargedelay")]
    public float RechargeDelay = 2f;

    [AutoNetworkedField]
    public EntityUid? OwnerUid = null;
}
