using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

using Content.Shared.Imperial.Changeling;

namespace Content.Server.Imperial.Changeling;

/// <summary>Способность с целью-существом.</summary>
public sealed partial class ImperialChangelingTargetPowerActionEvent : EntityTargetActionEvent
{
    [DataField(required: true)]
    public ProtoId<ImperialChangelingPowerPrototype> Power;
}
