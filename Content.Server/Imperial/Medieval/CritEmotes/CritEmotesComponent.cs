using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Body.Components;

[RegisterComponent]
public sealed partial class SoftCritEmotesComponent : Component
{
    [DataField]
    public ProtoId<EmotePrototype>[] Emotes = [];

    [DataField]
    public float MinDamage = 70f;

    [DataField]
    public bool Emote = true;

    [DataField]
    public bool Heartbeat = true;

    public TimeSpan NextEmoteUpdate = TimeSpan.Zero;
    public TimeSpan NextHeartbeatUpdate = TimeSpan.Zero;
}
