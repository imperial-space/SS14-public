using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Morph;

/// <summary>Выбор в окне: индекс облика или <see cref="OriginalForm"/>.</summary>
[Serializable, NetSerializable]
public sealed class MorphFormChosenMessage : BoundUserInterfaceMessage
{
    public const int OriginalForm = -1;

    public int Index { get; }

    public MorphFormChosenMessage(int index)
    {
        Index = index;
    }
}
