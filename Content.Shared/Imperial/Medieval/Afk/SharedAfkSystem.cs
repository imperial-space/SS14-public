using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Afk;

public sealed class SharedAfkSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
    }
}


[ByRefEvent]
public readonly struct MedievalAfkEvent
{
    public readonly ICommonSession Session;

    public MedievalAfkEvent(ICommonSession playerSession)
    {
        Session = playerSession;
    }
}

[ByRefEvent]
public readonly struct MedievalUnAfkEvent
{
    public readonly ICommonSession Session;

    public MedievalUnAfkEvent(ICommonSession playerSession)
    {
        Session = playerSession;
    }
}

[Serializable, NetSerializable]
public sealed class MedievalPlayerActionEvent : EntityEventArgs
{
    public readonly NetUserId SessionId;

    public MedievalPlayerActionEvent(NetUserId sessionId)
    {
        SessionId = sessionId;
    }
}
