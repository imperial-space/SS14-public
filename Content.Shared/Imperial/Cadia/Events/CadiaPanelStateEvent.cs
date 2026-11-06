using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public sealed class CadiaPanelStateEvent : EntityEventArgs
{
    public bool Active;
    public CadiaWarPhase Phase;
    public int SecondsLeft;
    public int PylonsAlive;
    public int PylonsTotal;
    public int Warp;
    public int ImperiumAlive;
    public int ChaosAlive;
    public int ImperiumSeats;
    public int ChaosSeats;
    public bool ImperiumForced;
    public bool ChaosForced;
    public int Saved;
    public bool VeilUp;
    public int RefugeesSaved;
    public int RefugeesTotal;
    public List<CadiaPanelPlayer> Players = new();
}
