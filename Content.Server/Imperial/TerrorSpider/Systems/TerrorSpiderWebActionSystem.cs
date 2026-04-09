using Content.Server.Popups;
using Content.Shared.Imperial.TerrorSpider.Events;
using Content.Shared.Maps;
using Content.Shared.Spider;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider.Systems;

public sealed class TerrorSpiderWebActionSystem : EntitySystem
{
    private const string GuardianBarrierPrototype = "SpiderWebTerrorGuardianBarrier";
    private static readonly EntProtoId DefaultWebPrototype = "SpiderWeb";

    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private readonly HashSet<EntityUid> _webs = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderSpawnWebActionEvent>(OnSpawnWebAction);
    }

    private void OnSpawnWebAction(TerrorSpiderSpawnWebActionEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;
        var webPrototype = args.WebPrototype;

        if (string.IsNullOrWhiteSpace(webPrototype) && TryComp<SpiderComponent>(performer, out var spider))
            webPrototype = spider.WebPrototype;

        if (string.IsNullOrWhiteSpace(webPrototype))
            webPrototype = DefaultWebPrototype;

        var transform = Transform(performer);

        if (transform.GridUid == null)
        {
            _popup.PopupEntity(Loc.GetString("spider-web-action-nogrid"), performer, performer);
            return;
        }

        if (IsTileBlockedByWeb(transform.Coordinates, webPrototype))
        {
            _popup.PopupEntity(Loc.GetString("spider-web-action-fail"), performer, performer);
            return;
        }

        Spawn(webPrototype, transform.Coordinates);
        _popup.PopupEntity(Loc.GetString("spider-web-action-success"), performer, performer);
        args.Handled = true;
    }

    private bool IsTileBlockedByWeb(EntityCoordinates coords, string targetPrototype)
    {
        _webs.Clear();
        _turf.GetEntitiesInTile(coords, _webs);

        var hasAnyWeb = false;

        foreach (var entity in _webs)
        {
            if (HasComp<SpiderWebObjectComponent>(entity))
            {
                hasAnyWeb = true;

                if (MetaData(entity).EntityPrototype?.ID == targetPrototype)
                    return true;
            }
        }

        if (targetPrototype == GuardianBarrierPrototype)
            return false;

        return hasAnyWeb;
    }
}
