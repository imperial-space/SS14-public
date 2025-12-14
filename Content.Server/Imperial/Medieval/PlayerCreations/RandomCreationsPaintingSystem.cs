using System.Linq;
using Content.Server.Database;
using Content.Server.Imperial.Medieval.PlayerCreations.Administration;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.CCVar;
using Content.Shared.Imperial.Medieval.PlayerCreations;
using Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.PlayerCreations;
public sealed class RandomCreationsPaintingSystem : EntitySystem
{

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CreationsSystem _creations = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private Dictionary<string, int> _paintngsSpawned = new();


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomCreationsPaintingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStart);
    }

    public void OnRoundStart(RoundStartedEvent args)
    {
        _paintngsSpawned = new();
    }

    public async void OnMapInit(EntityUid uid, RandomCreationsPaintingComponent comp, MapInitEvent args)
    {
        var maxPaintings = _cfg.GetCVar(MedievalCCVars.CreationsMaxPaintings);
        var acceptedPaintings = await _creations.GetAcceptedPaintingsMessages();

        foreach (var spawned in _paintngsSpawned)
        {
            if (spawned.Value < maxPaintings)
                continue;
            var toRemove = acceptedPaintings
                .FirstOrDefault(v => PaintingHelper.ColorsToString(v.Painting) == spawned.Key);

            if (toRemove != null)
                acceptedPaintings.Remove(toRemove);
        }

        if (acceptedPaintings.Count <= 0)
        {
            QueueDel(uid);
            return;
        }

        var selected = _random.Pick(acceptedPaintings);
        var stringColors = PaintingHelper.ColorsToString(selected.Painting);
        _paintngsSpawned.TryAdd(stringColors, 0);
        _paintngsSpawned[stringColors] += 1;

        var painting = Spawn(comp.PaintingPrototype, Transform(uid).Coordinates);
        if (TryComp<CanvasComponent>(painting, out var canvas))
        {
            canvas.Texture = selected.Painting;
            RaiseNetworkEvent(new CanvasTextureChangedEvent(GetNetEntity(painting), canvas.Texture));
            canvas.Dirty();
        }
        _metaData.SetEntityName(painting, $"{selected.Name}");
        _metaData.SetEntityDescription(painting, $"{selected.Description} - {selected.Author}");
        QueueDel(uid);

    }
}
