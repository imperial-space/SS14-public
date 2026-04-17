using Content.Server.GameTicking.Rules;
using Content.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(BloodBrotherRuleSystem))]
public sealed partial class BloodBrotherRuleComponent : Component
{
    [DataField]
    public ProtoId<WeightedRandomPrototype> SharedTargetObjectivePool = "TraitorObjectiveGroupKill";

    [DataField]
    public EntProtoId EscapeObjective = "EscapeShuttleObjective";

    [DataField]
    public SoundSpecifier GreetSoundNotification = new SoundPathSpecifier("/Audio/Imperial/blood_brother/blood_brothers_intro.ogg");

    [ViewVariables]
    public readonly List<EntityUid> BloodBrotherMinds = new();

    [ViewVariables]
    public bool ObjectivesAssigned;
}