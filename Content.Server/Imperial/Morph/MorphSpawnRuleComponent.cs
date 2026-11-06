using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Morph;

/// <summary>events/spawn_morph: один морф из случайной вентиляции станции.</summary>
[RegisterComponent]
public sealed partial class MorphSpawnRuleComponent : Component
{
    [DataField]
    public EntProtoId MorphProto = "MobMorph";
}
