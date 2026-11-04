using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Morph;

/// <summary>datum/mimic_form: запомненный облик.</summary>
[DataDefinition, Serializable]
public sealed partial class MorphForm
{
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public EntityUid? Source;

    [DataField]
    public EntProtoId? Proto;
}
