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

public sealed class MorphSpawnRule : StationEventSystem<MorphSpawnRuleComponent>
{
    protected override void Started(EntityUid uid, MorphSpawnRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var station))
            return;

        var vents = new List<EntityCoordinates>();
        var locations = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
        while (locations.MoveNext(out _, out _, out var xform))
        {
            if (xform.Anchored && CompOrNull<StationMemberComponent>(xform.GridUid)?.Station == station)
                vents.Add(xform.Coordinates);
        }

        if (vents.Count == 0)
        {
            Log.Warning("No suitable vents detected for spawning morphs.");
            return;
        }

        // Призрак занимает морфа через гост-роль; пока его нет, морфом правит ИИ.
        var morph = Spawn(component.MorphProto, RobustRandom.Pick(vents));
        Log.Info($"Morph {ToPrettyString(morph)} was spawned by an event.");
    }
}
