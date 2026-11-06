using System.Linq;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>Цель аватара в домене: что делать на локации, с живым счётчиком.</summary>
[RegisterComponent]
public sealed partial class BitrunningObjectiveConditionComponent : Component
{
    [DataField]
    public BitrunningObjectiveKind Kind;

    [ViewVariables]
    public EntityUid? Server;

    [ViewVariables]
    public string? LastDescription;
}
