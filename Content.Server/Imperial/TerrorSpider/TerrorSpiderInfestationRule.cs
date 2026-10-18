using Robust.Shared.Random;
using Content.Server.Chat.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>TS_INFESTATION_*.</summary>
public enum TerrorSpiderInfestationType : byte
{
    Random,
    Prince,
    White,
    Princess,
    Queen,
}

/// <summary>
/// events/spider_terror.dm: пауки Ужаса из вентиляции, объявление через 240–270 секунд.
/// </summary>
[RegisterComponent]
public sealed partial class TerrorSpiderInfestationRuleComponent : Component
{
    /// <summary>infestation_type; Random — по населённости, как в wrappedstart().</summary>
    [DataField]
    public TerrorSpiderInfestationType Infestation = TerrorSpiderInfestationType.Random;

    [DataField]
    public TimeSpan MinAnnounceDelay = TimeSpan.FromSeconds(240);

    [DataField]
    public TimeSpan MaxAnnounceDelay = TimeSpan.FromSeconds(270);

    [DataField]
    public SoundSpecifier AnnouncementSound = new SoundPathSpecifier("/Audio/Imperial/TerrorSpider/outbreak_terror.ogg");

    [ViewVariables]
    public TimeSpan? AnnounceAt;
}

public sealed class TerrorSpiderInfestationRule : StationEventSystem<TerrorSpiderInfestationRuleComponent>
{
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    /// <summary>TS_MIDPOP_TRIGGER / TS_HIGHPOP_TRIGGER.</summary>
    private const int MidPop = 45;
    private const int HighPop = 80;

    protected override void Started(EntityUid uid, TerrorSpiderInfestationRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var type = component.Infestation;
        if (type == TerrorSpiderInfestationType.Random)
        {
            var clients = _players.PlayerCount;
            type = clients < MidPop
                ? RobustRandom.Pick(new[] { TerrorSpiderInfestationType.White, TerrorSpiderInfestationType.Princess })
                : clients < HighPop
                    ? RobustRandom.Pick(new[] { TerrorSpiderInfestationType.Prince, TerrorSpiderInfestationType.White, TerrorSpiderInfestationType.Princess })
                    : RobustRandom.Pick(new[] { TerrorSpiderInfestationType.Prince, TerrorSpiderInfestationType.White, TerrorSpiderInfestationType.Princess, TerrorSpiderInfestationType.Queen });
        }

        var (spider, count) = type switch
        {
            // Слабый: опасен в одиночном бою, но быстро выдыхается.
            TerrorSpiderInfestationType.Prince => (TerrorSpiderSystem.Prince, 1),
            // Зависит от того, скольких заразят.
            TerrorSpiderInfestationType.White => (TerrorSpiderSystem.White, 2),
            TerrorSpiderInfestationType.Princess => (TerrorSpiderSystem.Princess, 3),
            // Сильнейший — только при высоком онлайне.
            _ => (TerrorSpiderSystem.Queen, 1),
        };

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

        // Пауки занимаются призраками через гост-роль; без желающих их ведёт ИИ.
        for (var i = 0; i < count && vents.Count > 0; i++)
        {
            var vent = RobustRandom.PickAndTake(vents);
            Spawn(spider, vent);
        }

        component.AnnounceAt = Timing.CurTime + TimeSpan.FromSeconds(
            RobustRandom.NextFloat((float) component.MinAnnounceDelay.TotalSeconds, (float) component.MaxAnnounceDelay.TotalSeconds));
    }

    protected override void ActiveTick(EntityUid uid, TerrorSpiderInfestationRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.AnnounceAt is not { } at || Timing.CurTime < at)
            return;

        component.AnnounceAt = null;
        _chatSystem.DispatchGlobalAnnouncement(
            Loc.GetString("terror-spider-infestation-announcement"),
            Loc.GetString("terror-spider-infestation-sender"),
            announcementSound: component.AnnouncementSound,
            colorOverride: Color.Red);
    }
}
