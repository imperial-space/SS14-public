using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

[Serializable, NetSerializable]
public sealed class ImperialSpellbookState(string owner, int points, int maxPoints, int semiRandom, int fullRandom,
    List<ImperialSpellbookEntryState> entries) : BoundUserInterfaceState
{
    public string Owner = owner;
    public int Points = points;
    public int MaxPoints = maxPoints;
    public int SemiRandom = semiRandom;
    public int FullRandom = fullRandom;
    public List<ImperialSpellbookEntryState> Entries = entries;
}
