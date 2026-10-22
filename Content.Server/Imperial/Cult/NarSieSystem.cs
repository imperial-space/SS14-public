using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Cult.Components;
using Content.Server.RoundEnd;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cult;

/// <summary>Нар'Си (power/singularity/narsie.dm) и конец раунда культа.</summary>
public sealed class NarSieSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly CultSystem _cult = default!;
    [Dependency] private readonly CultConstructSystem _constructs = default!;
    [Dependency] private readonly CultStructureSystem _structures = default!;
    [Dependency] private readonly ImperialCultRuleSystem _rule = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly Content.Shared.Gibbing.GibbingSystem _gibbing = default!;

    private const float ConsumeRange = 12f;
    private const float GravPull = 10f;
    private const float PickTargetChance = 0.05f;
    private const float MesmerizeChance = 0.25f;
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CultNarSieComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CultNarSieComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnMapInit(Entity<CultNarSieComponent> ent, ref MapInitEvent args)
    {
        // narsie_spawn_animation(): 3.5 с анимации, потом движение.
        _cult.SetState(ent, "narsie_spawn_anim");
        ent.Comp.NextMove = _timing.CurTime + TimeSpan.FromSeconds(3.5);
        ent.Comp.SpawnAnimEnd = ent.Comp.NextMove;
        StartEndingTheRound(ent);
    }

    /// <summary>start_ending_the_round().</summary>
    private void StartEndingTheRound(Entity<CultNarSieComponent> ent)
    {
        var rule = _rule.EnsureRule();
        rule.Comp.NarSie = ent;
        rule.Comp.NarSieSummoned = true;
        ent.Comp.Started = true;

        foreach (var session in _players.Sessions)
            _cult.Message(session, Loc.GetString("cult-narsie-risen"));
        _audio.PlayGlobal(CultSounds.NarsieRisen, Filter.Broadcast(), true);

        var ghosts = Filter.Empty().AddWhereAttachedEntity(HasComp<GhostComponent>);
        _cult.MessageGhosts(ghosts, Loc.GetString("cult-narsie-risen-ghosts"));

        _cult.SetBloodTarget(ent, null, null);

        // Все культисты становятся жнецами (narsie_act).
        foreach (var cultist in _cult.CultistBodies().ToList())
            NarsieActMob(cultist);

        // souls_needed: живые некультисты на станции; soul_goal = round(1 + 0.75 * N).
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } mob || !HasComp<HumanoidProfileComponent>(mob) || _mobState.IsDead(mob) || _cult.IsCultist(mob))
                continue;
            if (EntityManager.System<Content.Server.Station.Systems.StationSystem>().GetOwningStation(mob) == null)
                continue;
            ent.Comp.SoulsNeeded.Add(mob);
        }
        ent.Comp.SoulGoal = (int) MathF.Round(1 + ent.Comp.SoulsNeeded.Count * 0.75f);

        rule.Comp.EndStage = 1;
        rule.Comp.NextEndStage = _timing.CurTime + TimeSpan.FromSeconds(15);
        _rule.UpdateObjectiveTexts(rule.Comp);
    }

    /// <summary>fall_of_the_harbinger().</summary>
    private void OnTerminating(Entity<CultNarSieComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!_rule.TryGetRule(out var rule) || rule.Value.Comp.NarSie != ent.Owner)
            return;
        rule.Value.Comp.NarSie = null;
        rule.Value.Comp.NarSieSummoned = false;
        rule.Value.Comp.NarSieKilled = true;
        var line = _random.Pick(new[] { "Nooooo...", "Not die. How-", "Die. Mort-", "Sas tyen re-" });
        foreach (var session in _players.Sessions)
            _cult.Message(session, Loc.GetString("cult-narsie-fall", ("line", line)));
        _audio.PlayGlobal(CultSounds.DemonDies, Filter.Broadcast(), true, AudioParams.Default.WithVolume(CultSystem.Db(50)));
        _cult.UnsetBloodTarget(silent: true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<CultNarSieComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var narsie, out var xform))
        {
            if (now >= narsie.SpawnAnimEnd)
                Glide((uid, narsie), xform, frameTime);
            if (now < narsie.NextMove)
                continue;
            if (narsie.NextRetarget == TimeSpan.Zero)
                _cult.SetState(uid, "narsie");
            narsie.NextMove = now + TimeSpan.FromSeconds(1);

            if (narsie.Target == null || TerminatingOrDeleted(narsie.Target.Value) || _random.Prob(PickTargetChance))
                PickCultist((uid, narsie));
            narsie.NextRetarget = now;

            if (_random.Prob(MesmerizeChance))
                Mesmerize(uid);

            ChooseDirection((uid, narsie), xform);
            Consume(uid, xform);
            Pull(uid);
        }

        UpdateEnding(now);
    }

    /// <summary>pickcultist(): культисты в приоритете, потом остальные, потом призраки.</summary>
    private void PickCultist(Entity<CultNarSieComponent> ent)
    {
        var mapId = Transform(ent).MapID;
        var cultists = new List<EntityUid>();
        var others = new List<EntityUid>();
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } mob || Transform(mob).MapID != mapId)
                continue;
            if (HasComp<GhostComponent>(mob))
                continue;
            if (!HasComp<HumanoidProfileComponent>(mob) || _mobState.IsDead(mob))
                continue;
            if (_cult.IsCultist(mob))
                cultists.Add(mob);
            else
                others.Add(mob);
        }

        EntityUid? target = cultists.Count > 0 ? _random.Pick(cultists) : others.Count > 0 ? _random.Pick(others) : null;
        if (target == null)
        {
            var ghosts = _players.Sessions.Select(s => s.AttachedEntity).Where(e => e != null && HasComp<GhostComponent>(e) && Transform(e.Value).MapID == mapId).ToList();
            if (ghosts.Count > 0)
                target = _random.Pick(ghosts);
        }
        if (target == null || target == ent.Comp.Target)
            return;

        if (ent.Comp.Target is { } old && !TerminatingOrDeleted(old))
            _cult.Message(old, Loc.GetString("cult-narsie-lost-interest"));
        ent.Comp.Target = target;
        _cult.Message(target.Value, Loc.GetString(HasComp<HumanoidProfileComponent>(target) ? "cult-narsie-hungers" : "cult-narsie-chosen"));
    }

    /// <summary>Выбор направления раз в секунду (singularity move).</summary>
    private void ChooseDirection(Entity<CultNarSieComponent> ent, TransformComponent xform)
    {
        var pos = _transform.GetWorldPosition(xform);
        if (ent.Comp.Target is { } target && !TerminatingOrDeleted(target) && Transform(target).MapID == xform.MapID && _random.Prob(0.6f))
        {
            var delta = _transform.GetWorldPosition(target) - pos;
            if (delta.LengthSquared() < 0.25f)
            {
                ent.Comp.MoveDir = Vector2.Zero;
                return;
            }
            var dir = delta.Normalized();
            ent.Comp.MoveDir = new Vector2(MathF.Round(dir.X), MathF.Round(dir.Y));
            return;
        }
        ent.Comp.MoveDir = _random.Pick(new[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY });
    }

    /// <summary>LONG_GLIDE: плавное скольжение вместо прыжков по тайлу.</summary>
    private void Glide(Entity<CultNarSieComponent> ent, TransformComponent xform, float frameTime)
    {
        if (ent.Comp.MoveDir == Vector2.Zero)
            return;
        var pos = _transform.GetWorldPosition(xform);
        _transform.SetWorldPosition((ent, xform), pos + ent.Comp.MoveDir * ent.Comp.Speed * frameTime);
    }

    /// <summary>consume(): турфы в радиусе 12 — narsie_act (20%), мобы на них — всегда.</summary>
    private void Consume(EntityUid narsie, TransformComponent xform)
    {
        var center = _transform.GetMapCoordinates(xform);

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(center, ConsumeRange).ToList())
        {
            if (HasComp<CultConstructComponent>(mob) || HasComp<CultShadeComponent>(mob) || _mobState.IsDead(mob))
                continue;
            NarsieActMob(mob);
        }

        var grids = new List<Entity<MapGridComponent>>();
        IoCManager.Resolve<IMapManager>().FindGridsIntersecting(center.MapId, Box2.CenteredAround(center.Position, new Vector2(ConsumeRange * 2)), ref grids);
        foreach (var grid in grids)
        {
            // Одним пакетом: SetTile по одному тайлу перестраивает чанк, свет и атмос сотни раз за секунду.
            var changes = new List<(Vector2i, Tile)>();
            foreach (var tile in _map.GetTilesIntersecting(grid, grid.Comp, new Circle(center.Position, ConsumeRange)))
            {
                if (!_random.Prob(0.2f))
                    continue;
                if (_structures.CultFloorTileFor(tile) is { } cult)
                    changes.Add((tile.GridIndices, cult));
            }
            if (changes.Count > 0)
                _map.SetTiles(grid, changes);
        }

        foreach (var ent in _lookup.GetEntitiesInRange(center, ConsumeRange, LookupFlags.Static).ToList())
        {
            if (!_random.Prob(0.2f) || TerminatingOrDeleted(ent))
                continue;
            if (HasComp<AirlockComponent>(ent) && !HasComp<CultRunedDoorComponent>(ent))
            {
                _structures.NarsieActAirlock(ent);
                continue;
            }
            if (_tags.HasTag(ent, WallTag) && MetaData(ent).EntityPrototype?.ID is { } id && id != "WallCult" && id != "WallCultArtificer"
                && !HasComp<CultBarrierComponent>(ent) && id != "CultWallForce")
            {
                var coords = Transform(ent).Coordinates;
                QueueDel(ent);
                Spawn("WallCult", coords);
            }
        }
    }

    /// <summary>mob/living/narsie_act(): игрок становится жнецом, тело — прахом.</summary>
    private void NarsieActMob(EntityUid mob)
    {
        if (TerminatingOrDeleted(mob))
            return;

        // Душа, которую жаждет Нар'Си: при наборе soul_goal — массовое обращение.
        var narsieQuery = EntityQueryEnumerator<CultNarSieComponent>();
        while (narsieQuery.MoveNext(out _, out var narsie))
        {
            if (!narsie.SoulsNeeded.Remove(mob))
                continue;
            narsie.Souls++;
            if (narsie.Souls == narsie.SoulGoal && _rule.TryGetRule(out var soulRule) && !soulRule.Value.Comp.Resolved)
            {
                soulRule.Value.Comp.Resolved = true;
                soulRule.Value.Comp.EndStage = 0;
                _audio.PlayGlobal(CultSounds.NukeAlarm, Filter.Broadcast(), true, AudioParams.Default.WithVolume(CultSystem.Db(70)));
                Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(12), () => PlayCinematic(CultCinematic.Arm));
            }
        }
        var coords = Transform(mob).Coordinates;
        if (HasComp<ActorComponent>(mob))
        {
            _constructs.MakeConstruct(CultConstructType.Harvester, mob, null, coords, fromMind: mob, stoneCaptured: false);
        }
        else
        {
            var type = _random.Pick(new[] { CultConstructType.Juggernaut, CultConstructType.Wraith, CultConstructType.Artificer, CultConstructType.Proteon });
            if (CultConstructSystem.ConstructProtos.TryGetValue(type, out var proto))
                Spawn(proto, coords);
        }
        Spawn("Ash", coords);
        _gibbing.Gib(mob);
    }

    /// <summary>mesmerize(): не-культисты в поле зрения оглушены на 6 с.</summary>
    private void Mesmerize(EntityUid narsie)
    {
        var origin = _transform.GetMapCoordinates(narsie);
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(origin, ConsumeRange))
        {
            if (_cult.IsCultAligned(mob) || !_mobState.IsAlive(mob) || !HasComp<HumanoidProfileComponent>(mob))
                continue;
            if (!_examine.InRangeUnOccluded(origin, _transform.GetMapCoordinates(mob), ConsumeRange, null))
                continue;
            _cult.Message(mob, Loc.GetString("cult-narsie-mesmerize", ("narsie", narsie)));
            _cult.Stun(mob, 6);
        }
    }

    /// <summary>grav_pull 10: незакреплённое тянет к Нар'Си.</summary>
    private void Pull(EntityUid narsie)
    {
        var center = _transform.GetWorldPosition(narsie);
        var mapId = Transform(narsie).MapID;
        foreach (var ent in _lookup.GetEntitiesInRange(new MapCoordinates(center, mapId), GravPull, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (ent == narsie || HasComp<GhostComponent>(ent) || _cult.IsCultAligned(ent))
                continue;
            var xform = Transform(ent);
            if (xform.Anchored || xform.ParentUid != xform.GridUid && xform.ParentUid != xform.MapUid)
                continue;
            var pos = _transform.GetWorldPosition(xform);
            var delta = center - pos;
            if (delta.LengthSquared() < 1f)
                continue;
            _transform.SetWorldPosition((ent, xform), pos + delta.Normalized() * 0.5f);
        }
    }

    /// <summary>begin_the_end() → narsie_end_begin_check → … → cult_ending_helper.</summary>
    private void UpdateEnding(TimeSpan now)
    {
        if (!_rule.TryGetRule(out var ruleEnt))
            return;
        var rule = ruleEnt.Value.Comp;
        if (rule.EndStage == 0 || now < rule.NextEndStage)
            return;

        var alive = rule.NarSie != null && !TerminatingOrDeleted(rule.NarSie.Value);
        switch (rule.EndStage)
        {
            case 1:
                if (!alive)
                {
                    _cult.Announce(Loc.GetString("cult-narsie-end-1-fail"), CultSounds.Notice1);
                    Finish(rule, failed: true);
                    return;
                }
                _cult.Announce(Loc.GetString("cult-narsie-end-1"), CultSounds.CultRiseAnnounce);
                rule.EndStage = 2;
                rule.NextEndStage = now + TimeSpan.FromSeconds(50);
                break;
            case 2:
                if (!alive)
                {
                    _cult.Announce(Loc.GetString("cult-narsie-end-2-fail"), CultSounds.Notice1);
                    Finish(rule, failed: true);
                    return;
                }
                _cult.Announce(Loc.GetString("cult-narsie-end-2"), null);
                rule.EndStage = 3;
                rule.NextEndStage = now + TimeSpan.FromSeconds(5);
                break;
            case 3:
                // narsie_start_destroy_station(): шаттл заблокирован.
                if (_roundEnd.ExpectedCountdownEnd != null)
                    _roundEnd.CancelRoundEndCountdown(forceRecall: true);
                rule.EndStage = 4;
                rule.NextEndStage = now + TimeSpan.FromMinutes(1);
                break;
            case 4:
                if (!alive)
                {
                    _cult.Announce(Loc.GetString("cult-narsie-end-3-fail"), CultSounds.Notice1);
                    Finish(rule, failed: true);
                    return;
                }
                if (!rule.Resolved)
                {
                    rule.Resolved = true;
                    _chatAnnounceRed(Loc.GetString("cult-narsie-end-3"));
                    _audio.PlayGlobal(CultSounds.NukeAlarm, Filter.Broadcast(), true, AudioParams.Default.WithVolume(CultSystem.Db(70)));
                }
                rule.EndStage = 5;
                rule.NextEndStage = now + TimeSpan.FromSeconds(12);
                break;
            case 5:
                Finish(rule, failed: false);
                break;
        }
    }

    private void _chatAnnounceRed(string text)
    {
        EntityManager.System<Content.Server.Chat.Systems.ChatSystem>().DispatchGlobalAnnouncement(text,
            Loc.GetString("cult-narsie-security-sender"), true, CultSounds.Notice1, Color.Red);
    }

    private void Finish(ImperialCultRuleComponent rule, bool failed)
    {
        rule.EndStage = 0;
        if (!failed)
        {
            PlayCinematic(CultCinematic.Nuke);
            return;
        }
        rule.NarSieKilled = true;
        // addtimer(cult_ending_helper(CULT_FAILURE_NARSIE_KILLED), 2 SECONDS)
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(2), () => PlayCinematic(CultCinematic.Fail));
    }

    /// <summary>cult_ending_helper(): синематик, затем конец раунда.</summary>
    public void PlayCinematic(CultCinematic cinematic)
    {
        RaiseNetworkEvent(new CultCinematicEvent(cinematic));
        float end;
        switch (cinematic)
        {
            case CultCinematic.Arm:
                Sound(2.5f, CultSounds.EnterBlood);
                Sound(5.3f, CultSounds.TerminalOff);
                Sound(7.3f, CultSounds.Ghost);
                // Манифест закрывает синематик: даём досмотреть поглощение станции.
                end = 20f;
                break;
            case CultCinematic.Fail:
                Sound(2f, CultSounds.NarsieRises);
                Sound(8f, CultSounds.ExplosionDistant);
                Sound(9f, CultSounds.DemonDies);
                end = 12f;
                break;
            default:
                Sound(3.5f, CultSounds.ExplosionDistant);
                // intro_cult_fleet (3.5 с) + station_explode_fleet_fade_red (7.9 с) + итоговый кадр.
                end = 13.4f;
                break;
        }
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(end), () => _roundEnd.EndRound());
    }

    private void Sound(float delay, SoundSpecifier sound)
    {
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(delay), () => _audio.PlayGlobal(sound, Filter.Broadcast(), true));
    }
}
