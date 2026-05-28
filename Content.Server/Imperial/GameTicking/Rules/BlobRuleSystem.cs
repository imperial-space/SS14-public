using Content.Server.AlertLevel;
using Content.Server.Antag;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Imperial.Blob;
using Content.Server.Imperial.Blob.Components;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Server.Player;
using Robust.Shared.Map.Components;

namespace Content.Server.GameTicking.Rules;

public sealed class BlobRuleSystem : StationEventSystem<BlobRuleComponent>
{
    private static readonly Color BlobBriefingColor = Color.FromHex("#5acb74");
    private static readonly Color BlobAlertColor = Color.FromHex("#d94b4b");
    private static readonly Color BlobResolvedColor = Color.FromHex("#5acb74");
    private static readonly SoundPathSpecifier BlobAnnouncementAlarm = new("/Audio/Imperial/blob/sound_effects_siren-spooky.ogg");
    private static readonly SoundPathSpecifier BlobAnnouncementVoice = new("/Audio/Imperial/blob/sound_AI_outbreak_blob.ogg");

    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BlobOvermindSystem _blobOvermind = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagSelected);
        SubscribeLocalEvent<BlobStructureComponent, EntityTerminatingEvent>(OnBlobStructureTerminating);
    }

    protected override void Started(EntityUid uid, BlobRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!component.SpawnGhostRoleAtVentOnStart)
            return;

        SpawnMouseGhostRoleAtRandomVent(uid, component);
    }

    protected override void ActiveTick(EntityUid uid, BlobRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.VictoryTriggered || GameTicker.RunLevel != GameRunLevel.InRound)
            return;

        TrySendBiohazardAnnouncement(uid, component, frameTime);

        component.VictoryAccumulator += frameTime;
        if (component.VictoryAccumulator < component.VictoryCheckInterval)
            return;

        component.VictoryAccumulator -= component.VictoryCheckInterval;

        var gammaTiles = 0;
        EntityUid? gammaBlobMind = null;

        foreach (var mindId in component.BlobMinds)
        {
            var infectedTiles = GetOwnedStationTileCount(mindId);

            if (infectedTiles > gammaTiles)
            {
                gammaTiles = infectedTiles;
                gammaBlobMind = mindId;
            }

            if (infectedTiles < component.WinningInfectedStationTiles)
                continue;

            component.VictoryTriggered = true;
            Dirty(uid, component);
            GameTicker.EndRound(Loc.GetString("blob-round-end-victory", ("count", infectedTiles)));
            return;
        }

        if (!component.GammaTriggered &&
            gammaBlobMind is { } blobMind &&
            gammaTiles >= component.GammaThreshold)
        {
            TryTriggerGammaAlert(uid, component, blobMind, gammaTiles);
        }
    }

    private void OnAfterAntagSelected(Entity<BlobRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!_mind.TryGetMind(args.EntityUid, out var mindId, out var mind))
            return;

        EnsureBlobObjective(mindId, mind);

        if (ent.Comp.StartAsCarrier)
        {
            var carrier = EnsureComp<BlobMouseComponent>(args.EntityUid);
            carrier.Chemical = ent.Comp.StartingChemical;
            carrier.SourceRule = ent.Owner;
            Dirty(args.EntityUid, carrier);

            if (mind.UserId == null || !_players.TryGetSessionById(mind.UserId.Value, out var carrierSession))
                return;

            _antag.SendBriefing(carrierSession, Loc.GetString("blob-role-greeting-carrier"), BlobBriefingColor, ent.Comp.GreetSoundNotification);
            return;
        }

        var coordinates = Transform(args.EntityUid).Coordinates;
        var mouse = Spawn(ent.Comp.MousePrototype, coordinates);
        if (TryComp<BlobMouseComponent>(mouse, out var blobMouse))
        {
            blobMouse.Chemical = ent.Comp.StartingChemical;
            blobMouse.SourceRule = ent.Owner;
            Dirty(mouse, blobMouse);
        }

        _mind.TransferTo(mindId, mouse, ghostCheckOverride: true, mind: mind);

        if (args.EntityUid != mouse && Exists(args.EntityUid))
            QueueDel(args.EntityUid);

        if (mind.UserId == null || !_players.TryGetSessionById(mind.UserId.Value, out var session))
            return;

        _antag.SendBriefing(session, Loc.GetString("blob-role-greeting-mouse"), BlobBriefingColor, ent.Comp.GreetSoundNotification);
    }

    public bool TryStartBlob(EntityUid sourceUid, EntityUid mindId, MindComponent mind, BlobChemicalType? chemical = null, bool deleteSource = true, bool announce = true, EntityUid? ruleOverride = null)
    {
        if (Deleted(sourceUid))
            return false;

        EnsureBlobObjective(mindId, mind);

        BlobRuleComponent? rule = null;
        EntityUid? ruleUid = null;

        if (ruleOverride is { } overrideUid && TryComp<BlobRuleComponent>(overrideUid, out var overrideRule))
        {
            ruleUid = overrideUid;
            rule = overrideRule;
        }
        else
        {
            var rules = EntityQueryEnumerator<BlobRuleComponent>();
            while (rules.MoveNext(out var currentRuleUid, out var currentRule))
            {
                ruleUid = currentRuleUid;
                rule = currentRule;
                break;
            }
        }

        var coordinates = Transform(sourceUid).Coordinates;
        var overmindPrototype = rule?.OvermindPrototype ?? "MobBlobOvermind";
        var corePrototype = rule?.CorePrototype ?? "BlobCore";
        var selectedChemical = chemical ?? rule?.StartingChemical ?? BlobChemicalType.Sorium;

        var overmind = Spawn(overmindPrototype, coordinates);
        var core = Spawn(corePrototype, coordinates);

        if (TryComp<BlobOvermindComponent>(overmind, out var overmindComp))
        {
            overmindComp.BlobId = mindId;
            overmindComp.Chemical = selectedChemical;
            Dirty(overmind, overmindComp);
        }

        if (TryComp<BlobStructureComponent>(core, out var blobStructure))
        {
            blobStructure.OwnerMind = mindId;
            Dirty(core, blobStructure);
        }

        _mind.TransferTo(mindId, overmind, ghostCheckOverride: true, mind: mind);

        if (TryComp<BlobOvermindComponent>(overmind, out overmindComp))
            _blobOvermind.InitializeOvermind(overmind, overmindComp, refreshActions: true);

        if (deleteSource && sourceUid != overmind && Exists(sourceUid))
            QueueDel(sourceUid);

        if (ruleUid != null && rule != null && !rule.BlobMinds.Contains(mindId))
        {
            var previousCount = rule.BlobMinds.Count;
            rule.BlobMinds.Add(mindId);

            if (previousCount == 0)
            {
                rule.BiohazardAnnouncementAccumulator = 0f;
                rule.BiohazardAnnouncementSent = false;
            }

            Dirty(ruleUid.Value, rule);
        }

        if (!announce || mind.UserId == null || !_players.TryGetSessionById(mind.UserId.Value, out var session))
            return true;

        var sound = rule?.GreetSoundNotification;
        _antag.SendBriefing(session, Loc.GetString("blob-role-greeting"), BlobBriefingColor, sound);
        return true;
    }

    private void OnBlobStructureTerminating(EntityUid uid, BlobStructureComponent comp, EntityTerminatingEvent args)
    {
        if (!IsBlobCorePrototype(MetaData(uid).EntityPrototype?.ID))
            return;

        if (comp.OwnerMind is not { } blobId)
            return;

        var rules = EntityQueryEnumerator<BlobRuleComponent>();
        while (rules.MoveNext(out var ruleUid, out var rule))
        {
            if (CountOwnedCores(blobId, uid) > 0)
            {
                var overminds = EntityQueryEnumerator<BlobOvermindComponent, MindContainerComponent>();
                while (overminds.MoveNext(out _, out var overmind, out var mindContainer))
                {
                    if (overmind.BlobId != blobId || mindContainer.Mind is not { } overmindMind)
                        continue;

                    if (!_mind.TryGetMind(overmindMind, out _, out var survivingMind) ||
                        survivingMind.UserId == null ||
                        !_players.TryGetSessionById(survivingMind.UserId.Value, out var survivingSession))
                    {
                        continue;
                    }

                    _antag.SendBriefing(survivingSession, Loc.GetString("blob-core-destroyed-secondary"), Color.Orange, null);
                }

                break;
            }

            if (!rule.BlobMinds.Remove(blobId))
                continue;

            var hasStation = TryGetBlobStation(blobId, out var station);

            if (rule.BlobMinds.Count == 0)
            {
                rule.BiohazardAnnouncementAccumulator = 0f;
                rule.BiohazardAnnouncementSent = false;
                rule.GammaTriggered = false;
            }

            _blobOvermind.NeutralizeBlob(blobId);

            if (hasStation)
            {
                _alertLevel.SetLevel(station, "green", true, true, force: true);
                _chat.DispatchStationAnnouncement(
                    station,
                    Loc.GetString("blob-centcom-neutralized-announcement"),
                    Loc.GetString("comms-console-announcement-title-centcom"),
                    playDefaultSound: false,
                    colorOverride: BlobResolvedColor);
            }

            Dirty(ruleUid, rule);
            break;
        }
    }

    private int CountOwnedCores(EntityUid mindId, EntityUid? exclude = null)
    {
        var cores = 0;
        var structures = EntityQueryEnumerator<BlobStructureComponent, MetaDataComponent>();
        while (structures.MoveNext(out var structureUid, out var structure, out var meta))
        {
            if (exclude == structureUid)
                continue;

            if (structure.OwnerMind != mindId)
                continue;

            if (!IsBlobCorePrototype(meta.EntityPrototype?.ID))
                continue;

            cores++;
        }

        return cores;
    }

    public int GetOwnedStationTileCount(EntityUid mindId)
    {
        var stationGrids = new HashSet<EntityUid>();
        var stations = EntityQueryEnumerator<StationDataComponent>();
        while (stations.MoveNext(out _, out var station))
        {
            foreach (var grid in station.Grids)
            {
                stationGrids.Add(grid);
            }
        }

        var infectedTiles = new HashSet<(EntityUid, Vector2i)>();
        var structures = EntityQueryEnumerator<BlobStructureComponent, TransformComponent>();
        while (structures.MoveNext(out _, out var structure, out var xform))
        {
            if (structure.OwnerMind != mindId)
                continue;

            if (xform.GridUid is not { } gridUid)
                continue;

            if (!stationGrids.Contains(gridUid))
                continue;

            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
            infectedTiles.Add((gridUid, tile));
        }

        return infectedTiles.Count;
    }

    private void EnsureBlobObjective(EntityUid mindId, MindComponent mind)
    {
        if (_mind.TryFindObjective((mindId, mind), "BlobInfectStationObjective", out _))
            return;

        if (_mind.TryAddObjective(mindId, mind, "BlobInfectStationObjective"))
            return;

        Log.Warning($"Failed to add BlobInfectStationObjective to {ToPrettyString(mindId)}");
    }

    private void TrySendBiohazardAnnouncement(EntityUid uid, BlobRuleComponent component, float frameTime)
    {
        if (component.BiohazardAnnouncementSent || !TryGetBlobStation(component, out var station))
            return;

        component.BiohazardAnnouncementAccumulator += frameTime;
        if (component.BiohazardAnnouncementAccumulator < component.BiohazardAnnouncementDelay)
            return;

        component.BiohazardAnnouncementSent = true;

        _chat.DispatchStationAnnouncement(
            station,
            Loc.GetString("blob-centcom-biohazard-announcement"),
            Loc.GetString("comms-console-announcement-title-centcom"),
            announcementSound: BlobAnnouncementAlarm,
            colorOverride: BlobAlertColor);

        if (TryComp<StationDataComponent>(station, out var stationData))
            _audio.PlayGlobal(BlobAnnouncementVoice, _station.GetInStation(stationData), true);

        Dirty(uid, component);
    }

    private void TryTriggerGammaAlert(EntityUid uid, BlobRuleComponent component, EntityUid blobMind, int infectedTiles)
    {
        if (!TryGetBlobStation(blobMind, out var station))
            return;

        component.GammaTriggered = true;
        component.BiohazardAnnouncementSent = true;

        _alertLevel.SetLevel(station, "gamma", true, true, force: true);
        _chat.DispatchStationAnnouncement(
            station,
            Loc.GetString("blob-gamma-threshold-announcement", ("count", infectedTiles)),
            Loc.GetString("comms-console-announcement-title-centcom"),
            playDefaultSound: false,
            colorOverride: BlobAlertColor);

        Dirty(uid, component);
    }

    private static bool IsBlobCorePrototype(string? prototype)
    {
        return prototype == "BlobCore" || prototype == "BlobCoreGhostRole";
    }

    private bool TryGetBlobStation(BlobRuleComponent component, out EntityUid station)
    {
        foreach (var mindId in component.BlobMinds)
        {
            if (!TryGetBlobStation(mindId, out var owningStation))
                continue;

            station = owningStation;
            return true;
        }

        station = EntityUid.Invalid;
        return false;
    }

    private bool TryGetBlobStation(EntityUid blobMind, out EntityUid station)
    {
        var structures = EntityQueryEnumerator<BlobStructureComponent>();
        while (structures.MoveNext(out var uid, out var structure))
        {
            if (structure.OwnerMind != blobMind)
                continue;

            if (_station.GetOwningStation(uid) is not { } owningStation)
                continue;

            station = owningStation;
            return true;
        }

        station = EntityUid.Invalid;
        return false;
    }

    private void SpawnMouseGhostRoleAtRandomVent(EntityUid uid, BlobRuleComponent component)
    {
        if (!TryGetRandomStation(out var station))
            return;

        var locations = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
        var validLocations = new List<EntityCoordinates>();
        while (locations.MoveNext(out _, out _, out var transform))
        {
            if (CompOrNull<StationMemberComponent>(transform.GridUid)?.Station != station)
                continue;

            validLocations.Add(transform.Coordinates);
        }

        if (validLocations.Count == 0)
            return;

        var spawner = Spawn(component.GhostRoleSpawnerPrototype, validLocations[RobustRandom.Next(validLocations.Count)]);
        var source = EnsureComp<BlobRuleSourceComponent>(spawner);
        source.Rule = uid;
        source.Chemical = component.StartingChemical;
        Dirty(spawner, source);
    }
}