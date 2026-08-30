using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Alert;

namespace Content.Shared.Imperial.Aquila.TargetedEmote;

[RegisterComponent, NetworkedComponent]
public sealed partial class EmoteRequestComponent : Component
{
    [DataField]
    public EntityUid Requester;

    [DataField]
    public string EmoteId = string.Empty;

    [DataField]
    public TimeSpan ExpiresAt;

    [DataField]
    public ProtoId<AlertPrototype> Alert = "EmoteRequest";
}

public sealed partial class AcceptEmoteRequestAlertEvent : BaseAlertEvent;
