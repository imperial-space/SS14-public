using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic.Paths.Lock.KeyRing;

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
