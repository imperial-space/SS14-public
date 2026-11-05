using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

/// <summary>Summon Rune (create_rune): руна за 6 секунд.</summary>
public sealed partial class CultCreateRuneActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId Rune;

    /// <summary>Номер узора rune_spawn (rune1..rune7).</summary>
    [DataField]
    public int Pattern = 1;
}
