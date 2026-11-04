using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[DataDefinition]
public sealed partial class CadiaSummonEntry
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public int Cost = 10;

    /// <summary>Открывается только после падения половины пилонов или в развязке.</summary>
    [DataField]
    public bool RequiresFallenPylons;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);
}
