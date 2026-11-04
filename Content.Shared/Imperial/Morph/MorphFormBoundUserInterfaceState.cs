using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Morph;

[Serializable, NetSerializable]
public sealed class MorphFormBoundUserInterfaceState : BoundUserInterfaceState
{
    /// <summary>Имена запомненных обликов по порядку.</summary>
    public List<string> Forms { get; }

    /// <summary>Можно ли вернуться в истинный облик («Original Form»).</summary>
    public bool CanRestore { get; }

    public MorphFormBoundUserInterfaceState(List<string> forms, bool canRestore)
    {
        Forms = forms;
        CanRestore = canRestore;
    }
}
