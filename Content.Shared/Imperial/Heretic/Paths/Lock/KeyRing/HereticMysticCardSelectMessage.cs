using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic.Paths.Lock.KeyRing;

// ─── BUI Messages ────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class HereticMysticCardSelectMessage : BoundUserInterfaceMessage
{
    public string CardName;

    public HereticMysticCardSelectMessage(string cardName)
    {
        CardName = cardName;
    }
}
