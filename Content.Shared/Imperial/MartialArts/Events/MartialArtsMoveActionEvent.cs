using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MartialArts;

/// <summary>Приём, который заряжает следующую атаку: борьба, Каза Рук, дзюдо.</summary>
public sealed partial class MartialArtsMoveActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public string Move = string.Empty;
}
