using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic.KeyRing;

[Serializable, NetSerializable]
public enum HereticMysticCardUiKey { Key }

// ─── BUI State ───────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class HereticMysticCardBuiState : BoundUserInterfaceState
{
    public List<HereticAbsorbedCardInfo> Cards;
    public bool Inverted;

    public HereticMysticCardBuiState(List<HereticAbsorbedCardInfo> cards, bool inverted)
    {
        Cards = cards;
        Inverted = inverted;
    }
}

[Serializable, NetSerializable]
public sealed class HereticAbsorbedCardInfo
{
    public string Name = string.Empty;
    public List<string> AccessTags = new();
}

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
