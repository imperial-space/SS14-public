using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Секторы фронта: захват знамён, снабжение хозяина и точки высадки в удержанных секторах.</summary>
public sealed class CadiaSectorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;

    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);
    private static readonly SoundSpecifier TakenSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");
    private const float DepotPointsPerMinute = 3f;
    private const float WarpPerMinute = 1f;
    private const float CrowdBonus = 0.25f;
    private const int CrowdBonusLimit = 4;

    private readonly HashSet<Entity<CadiaFactionMemberComponent>> _inRange = new();
    private TimeSpan _next;
    private float _warpFraction;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaSectorComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<CadiaSectorComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInit(Entity<CadiaSectorComponent> ent, ref MapInitEvent args) => SyncSpawn(ent, null);

    private void OnExamined(Entity<CadiaSectorComponent> ent, ref ExaminedEvent args)
    {
        var owner = ent.Comp.Owner switch
        {
            CadiaFaction.Imperium => "cadia-sector-owner-imperium",
            CadiaFaction.Chaos => "cadia-sector-owner-chaos",
            _ => "cadia-sector-owner-none",
        };
        args.PushMarkup(Loc.GetString("cadia-sector-examine", ("letter", ent.Comp.Letter), ("owner", Loc.GetString(owner))));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _next)
            return;
        _next = now + Tick;
        if (!_rule.TryGetActiveWar(out var war) || war.Phase == CadiaWarPhase.Ended)
            return;

        var dt = (float) Tick.TotalSeconds;
        var infos = new List<CadiaSectorInfo>();
        var imperium = 0;
        var chaos = 0;
        var query = EntityQueryEnumerator<CadiaSectorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sector, out var xform))
        {
            UpdateSector((uid, sector), xform, war, dt);
            SyncSpawn((uid, sector), war);
            if (sector.Owner == CadiaFaction.Imperium)
                imperium++;
            else if (sector.Owner == CadiaFaction.Chaos)
                chaos++;
            infos.Add(new CadiaSectorInfo
            {
                Letter = sector.Letter,
                Owner = sector.Owner,
                Capturer = sector.Capturer,
                Progress = sector.Progress,
                Contested = sector.Contested,
                Position = _xform.GetWorldPosition(xform),
                Map = (int) xform.MapID,
            });
        }
        infos.Sort((a, b) => string.CompareOrdinal(a.Letter, b.Letter));
        if (!infos.SequenceEqual(war.Sectors))
        {
            war.Sectors = infos;
            _rule.DirtyWar();
        }
        UpdateLandmarks(war);

        // Снабжение идёт каждую секунду малыми долями, чтобы шкала депо не прыгала.
        var depots = EntityQueryEnumerator<CadiaDepotComponent>();
        while (depots.MoveNext(out var depot))
        {
            var held = depot.Faction == CadiaFaction.Imperium ? imperium : chaos;
            depot.Points = Math.Min(depot.MaxPoints, depot.Points + held * DepotPointsPerMinute * dt / 60f);
        }
        _warpFraction += chaos * WarpPerMinute * dt / 60f;
        if (_warpFraction >= 1f)
        {
            var whole = (int) _warpFraction;
            _warpFraction -= whole;
            _rule.AddWarp(whole);
        }
    }

    /// <summary>Пилоны, цитадель и маяк для подсказки цели: клиент сам выбирает ближайшее.</summary>
    private void UpdateLandmarks(CadiaWarStateComponent war)
    {
        var points = new List<CadiaLandmark>();
        void Add(CadiaLandmarkKind kind, EntityUid uid, int number)
        {
            if (TerminatingOrDeleted(uid))
                return;
            points.Add(new CadiaLandmark { Kind = kind, Number = number, Position = _xform.GetWorldPosition(uid), Map = (int) Transform(uid).MapID });
        }

        var pylons = EntityQueryEnumerator<CadiaPylonComponent>();
        while (pylons.MoveNext(out var uid, out var pylon))
            Add(CadiaLandmarkKind.Pylon, uid, pylon.Number);
        var citadels = EntityQueryEnumerator<CadiaCitadelComponent>();
        while (citadels.MoveNext(out var uid, out _))
            Add(CadiaLandmarkKind.Citadel, uid, 0);
        var beacons = EntityQueryEnumerator<CadiaEvacBeaconComponent>();
        while (beacons.MoveNext(out var uid, out _))
            Add(CadiaLandmarkKind.Beacon, uid, 0);

        if (points.SequenceEqual(war.Landmarks))
            return;
        war.Landmarks = points;
        _rule.DirtyWar();
    }

    private void UpdateSector(Entity<CadiaSectorComponent> sector, TransformComponent xform, CadiaWarStateComponent war, float dt)
    {
        _inRange.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, sector.Comp.Radius, _inRange);
        var imperium = 0;
        var chaos = 0;
        foreach (var member in _inRange)
        {
            if (member.Comp.Role == null || !_mobState.IsAlive(member))
                continue;
            if (member.Comp.Faction == CadiaFaction.Imperium)
                imperium++;
            else
                chaos++;
        }

        var contested = imperium > 0 && chaos > 0;
        var oldProgress = sector.Comp.Progress;
        var oldContested = sector.Comp.Contested;
        var oldCapturer = sector.Comp.Capturer;
        sector.Comp.Contested = contested;

        CadiaFaction? alone = contested ? null : imperium > 0 ? CadiaFaction.Imperium : chaos > 0 ? CadiaFaction.Chaos : null;
        // Пока поле пилонов не пало, Хаос ещё не на Кадии.
        if (alone == CadiaFaction.Chaos && war.Phase < CadiaWarPhase.Assault)
            alone = null;

        if (alone is { } side && side != sector.Comp.Owner)
        {
            if (sector.Comp.Capturer != side)
            {
                sector.Comp.Capturer = side;
                sector.Comp.Progress = 0f;
            }
            var crowd = Math.Min(side == CadiaFaction.Imperium ? imperium - 1 : chaos - 1, CrowdBonusLimit);
            sector.Comp.Progress += dt / (float) sector.Comp.CaptureTime.TotalSeconds * (1f + crowd * CrowdBonus);
            if (sector.Comp.Progress >= 1f)
                Capture(sector, side);
        }
        else if (!contested && sector.Comp.Progress > 0f)
        {
            // Без бойцов или при возвращении хозяина захват откатывается.
            sector.Comp.Progress = Math.Max(0f, sector.Comp.Progress - dt / (float) sector.Comp.CaptureTime.TotalSeconds);
            if (sector.Comp.Progress <= 0f)
                sector.Comp.Capturer = null;
        }

        if (!MathHelper.CloseTo(oldProgress, sector.Comp.Progress) || oldContested != contested || oldCapturer != sector.Comp.Capturer)
            Dirty(sector);
    }

    private void Capture(Entity<CadiaSectorComponent> sector, CadiaFaction side)
    {
        sector.Comp.Owner = side;
        sector.Comp.Capturer = null;
        sector.Comp.Progress = 0f;
        Dirty(sector);
        _rule.Announce(CadiaFaction.Imperium,
            Loc.GetString(side == CadiaFaction.Imperium ? "cadia-sector-taken-ours" : "cadia-sector-taken-theirs", ("letter", sector.Comp.Letter)), TakenSound);
        _rule.Announce(CadiaFaction.Chaos,
            Loc.GetString(side == CadiaFaction.Chaos ? "cadia-sector-taken-ours" : "cadia-sector-taken-theirs", ("letter", sector.Comp.Letter)), TakenSound);
        _rule.Feed(Loc.GetString(side == CadiaFaction.Imperium ? "cadia-feed-sector-imperium" : "cadia-feed-sector-chaos", ("letter", sector.Comp.Letter)), side);
    }

    /// <summary>Сюда можно высаживаться только со штурма, только своим и только пока за сектор не идёт бой.</summary>
    private void SyncSpawn(Entity<CadiaSectorComponent> sector, CadiaWarStateComponent? war)
    {
        if (!TryComp<CadiaSpawnPointComponent>(sector, out var point))
            return;
        point.Enabled = sector.Comp.Owner != null && !sector.Comp.Contested && war is { Phase: >= CadiaWarPhase.Assault };
        if (sector.Comp.Owner is { } owner)
            point.Faction = owner;
    }
}
