using Content.Server.Chat.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Xenomorph;

public sealed class XenomorphInfestationRule : StationEventSystem<XenomorphInfestationRuleComponent>
{
    [Dependency] private readonly ChatSystem _chatSystem = default!;

    protected override void Started(EntityUid uid, XenomorphInfestationRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
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
            return;

        // 50 % шанс получить вторую личинку бесплатно.
        var count = RobustRandom.Prob(0.5f) ? 2 : 1;
        for (var i = 0; i < count; i++)
        {
            Spawn(component.Spawner, RobustRandom.Pick(vents));
        }

        component.AnnounceAt = Timing.CurTime + TimeSpan.FromSeconds(
            RobustRandom.NextFloat((float) component.MinAnnounceDelay.TotalSeconds, (float) component.MaxAnnounceDelay.TotalSeconds));
    }

    protected override void ActiveTick(EntityUid uid, XenomorphInfestationRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.AnnounceAt is not { } at || Timing.CurTime < at)
            return;

        component.AnnounceAt = null;
        _chatSystem.DispatchGlobalAnnouncement(
            Loc.GetString("xeno-infestation-announcement"),
            Loc.GetString("xeno-infestation-sender"),
            announcementSound: component.AnnouncementSound,
            colorOverride: Color.Gold);
    }
}
