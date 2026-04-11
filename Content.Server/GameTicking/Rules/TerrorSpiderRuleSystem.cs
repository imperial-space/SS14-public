using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.GameTicking.Rules;

public sealed class TerrorSpiderRuleSystem : GameRuleSystem<TerrorSpiderRuleComponent>
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagSelected);
    }

    private void OnAfterAntagSelected(Entity<TerrorSpiderRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!_mind.TryGetMind(args.EntityUid, out var mindId, out var mind))
            return;

        var station = _station.GetOwningStation(args.EntityUid);
        var coordinates = TryGetRandomVentCoordinates(station, out var ventCoordinates)
            ? ventCoordinates
            : Transform(args.EntityUid).Coordinates;

        var queen = Spawn(ent.Comp.QueenPrototype, coordinates);
        _mind.TransferTo(mindId, queen, ghostCheckOverride: true, mind: mind);

        if (args.EntityUid != queen && Exists(args.EntityUid))
            QueueDel(args.EntityUid);
    }

    private bool TryGetRandomVentCoordinates(EntityUid? station, out EntityCoordinates coordinates)
    {
        var locations = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
        var validLocations = new List<EntityCoordinates>();

        while (locations.MoveNext(out _, out _, out var transform))
        {
            if (station != null && CompOrNull<StationMemberComponent>(transform.GridUid)?.Station != station)
                continue;

            validLocations.Add(transform.Coordinates);
        }

        if (validLocations.Count == 0)
        {
            coordinates = default;
            return false;
        }

        coordinates = validLocations[_random.Next(validLocations.Count)];
        return true;
    }
}