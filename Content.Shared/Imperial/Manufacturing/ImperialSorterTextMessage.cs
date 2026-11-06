using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

[Serializable, NetSerializable]
public sealed class ImperialSorterTextMessage(int id, string text) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
    public readonly string Text = text;
}
