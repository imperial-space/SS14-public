using System.Linq;
using System.Numerics;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Mining;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.MeteorShield;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.MeteorShield;

/// <summary>Спутник метеоритного щита (obj/machinery/satellite/meteor_shield).</summary>
[RegisterComponent]
public sealed partial class MeteorShieldSatelliteComponent : Component
{
    [DataField]
    public string Mode = "M-SHIELD";

    /// <summary>kill_range: радиус, в котором спутник сбивает метеоры.</summary>
    [DataField]
    public int KillRange = 14;

    [DataField]
    public EntProtoId Beam = "ImperialMeteorShieldBeam";

    [DataField]
    public EntProtoId Signal = "ImperialMeteorShieldSignal";

    /// <summary>Звук луча. В SS13 луч щита беззвучен — добавлен лазерный выстрел.</summary>
    [DataField]
    public SoundSpecifier BeamSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/laser.ogg");

    [ViewVariables] public int Id;
    [ViewVariables] public bool Active;
    [ViewVariables] public TimeSpan NextCheck;
    [ViewVariables] public EntityUid? SignalEntity;
}

/// <summary>Консоль управления спутниками (computer/sat_control).</summary>
[RegisterComponent]
public sealed partial class SatelliteControlComponent : Component;

/// <summary>Тёмно-материальный метеор: отражает лучи щитов и роняет сингулярность.</summary>
[RegisterComponent]
public sealed partial class DarkMatteorComponent : Component
{
    /// <summary>meteorsound: при каждом ударе и при финальном взрыве (meteor_effect) для всех на карте.</summary>
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Imperial/MeteorShield/curse1.ogg");

    [DataField]
    public SoundSpecifier AnnouncementSound = new SoundPathSpecifier("/Audio/Imperial/MeteorShield/airraid.ogg");

    [ViewVariables] public TimeSpan NextImpactSound;
    [ViewVariables] public EntityUid? Station;
    [ViewVariables] public string? PreviousLevel;
    [ViewVariables] public bool Hit;
}

public sealed class MeteorShieldSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    /// <summary>coverage_goal цели «Станционные щиты».</summary>
    public const int CoverageGoal = 500;

    private const string PulsingQuality = "Pulsing";
    private const int ThresholdOne = 3;
    private const int ThresholdTwo = 6;
    private const int ThresholdThree = 7;
    private const int ThresholdFour = 10;
    private static readonly TimeSpan EmagCooldown = TimeSpan.FromMinutes(1);

    /// <summary>Как часто проверяется шанс внеочередного метеорного роя от взломанных щитов.</summary>
    private static readonly TimeSpan MeteorRollInterval = TimeSpan.FromMinutes(10);

    private static readonly string[] MeteorRules = { "MeteorSwarmSmall", "MeteorSwarmMedium", "MeteorSwarmLarge" };
    private static readonly EntProtoId DarkMatteor = "ImperialDarkMatteor";

    private int _globalId;
    private int _emaggedActive;
    private int _highestThreshold;
    private TimeSpan _emagCooldownEnd;
    private TimeSpan _nextMeteorRoll;
    private int _cachedCoverage;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);

        SubscribeLocalEvent<MeteorShieldSatelliteComponent, MapInitEvent>(OnSatelliteMapInit);
        SubscribeLocalEvent<MeteorShieldSatelliteComponent, ExaminedEvent>(OnSatelliteExamined);
        SubscribeLocalEvent<MeteorShieldSatelliteComponent, ActivateInWorldEvent>(OnSatelliteActivate);
        SubscribeLocalEvent<MeteorShieldSatelliteComponent, SatelliteToggleDoAfterEvent>(OnSatelliteToggleDoAfter);
        SubscribeLocalEvent<MeteorShieldSatelliteComponent, InteractUsingEvent>(OnSatelliteInteractUsing);
        SubscribeLocalEvent<MeteorShieldSatelliteComponent, GotEmaggedEvent>(OnSatelliteEmagged);
        SubscribeLocalEvent<MeteorShieldSatelliteComponent, ComponentShutdown>(OnSatelliteShutdown);

        SubscribeLocalEvent<SatelliteControlComponent, BoundUIOpenedEvent>((uid, _, _) => UpdateUi(uid));
        SubscribeLocalEvent<SatelliteControlComponent, SatelliteToggleMessage>(OnConsoleToggle);

        SubscribeLocalEvent<DarkMatteorComponent, DestructionEventArgs>(OnDarkMatteorDestroyed);
        SubscribeLocalEvent<DarkMatteorComponent, StartCollideEvent>(OnDarkMatteorCollide);
        SubscribeLocalEvent<DarkMatteorComponent, ComponentShutdown>(OnDarkMatteorShutdown);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _globalId = 0;
        _emaggedActive = 0;
        _highestThreshold = 0;
        _emagCooldownEnd = TimeSpan.Zero;
        _cachedCoverage = 0;
    }

    /// <summary>check_completion цели: выводим покрытие в итогах раунда.</summary>
    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        if (!EntityQuery<MeteorShieldSatelliteComponent>().Any())
            return;

        UpdateCoverage();
        ev.AddLine(Loc.GetString(_cachedCoverage >= CoverageGoal ? "meteor-shield-round-end-success" : "meteor-shield-round-end",
            ("coverage", _cachedCoverage), ("goal", CoverageGoal)));
    }

    #region Спутник

    private void OnSatelliteMapInit(Entity<MeteorShieldSatelliteComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Id = _globalId++;
        UpdateAppearance(ent);
    }

    private bool IsEmagged(EntityUid uid)
    {
        return _emag.CheckFlag(uid, EmagType.Interaction);
    }

    private void OnSatelliteExamined(Entity<MeteorShieldSatelliteComponent> ent, ref ExaminedEvent args)
    {
        var emagged = IsEmagged(ent);
        if (ent.Comp.Active)
        {
            args.PushMarkup(Loc.GetString("meteor-shield-examine-active"));
            args.PushMarkup(Loc.GetString(emagged ? "meteor-shield-examine-active-emagged" : "meteor-shield-examine-beeping"));
        }
        else
        {
            args.PushMarkup(Loc.GetString("meteor-shield-examine-inactive"));
            if (emagged)
                args.PushMarkup(Loc.GetString("meteor-shield-examine-inactive-emagged"));
        }
    }

    /// <summary>interact → toggle: 2 секунды ищем кнопку.</summary>
    private void OnSatelliteActivate(Entity<MeteorShieldSatelliteComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString(ent.Comp.Active ? "meteor-shield-looking-off" : "meteor-shield-looking-on"), ent, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2), new SatelliteToggleDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
        });
    }

    private void OnSatelliteToggleDoAfter(Entity<MeteorShieldSatelliteComponent> ent, ref SatelliteToggleDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        Toggle(ent, args.User);
    }

    private bool IsInSpace(EntityUid uid)
    {
        var coords = _xform.GetMapCoordinates(uid);
        if (!_mapManager.TryFindGridAt(coords, out var grid, out var gridComp))
            return true;

        var tile = _map.GetTileRef(grid, gridComp, _map.TileIndicesFor(grid, gridComp, coords));
        return tile.Tile.IsEmpty;
    }

    /// <summary>satellite/toggle + meteor_shield/toggle.</summary>
    private bool Toggle(Entity<MeteorShieldSatelliteComponent> ent, EntityUid? user)
    {
        if (!ent.Comp.Active && !IsInSpace(ent))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("meteor-shield-only-space", ("satellite", ent.Owner)), ent, user.Value);
            return false;
        }

        if (user != null)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.Active ? "meteor-shield-deactivate" : "meteor-shield-activate",
                ("satellite", ent.Owner)), ent, user.Value);
        }

        SetActive(ent, !ent.Comp.Active);

        if (IsEmagged(ent))
            UpdateEmaggedSatellite(ent, user);

        UpdateCoverage();
        UpdateAllConsoles();
        return true;
    }

    /// <summary>set_anchored: включённый спутник висит неподвижно.</summary>
    private void SetActive(Entity<MeteorShieldSatelliteComponent> ent, bool active)
    {
        ent.Comp.Active = active;
        if (TryComp<PhysicsComponent>(ent, out var physics))
        {
            if (active)
                _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);

            _physics.SetBodyType(ent, active ? BodyType.Static : BodyType.Dynamic, body: physics);
        }

        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<MeteorShieldSatelliteComponent> ent)
    {
        _appearance.SetData(ent, SatelliteVisuals.Active, ent.Comp.Active);
    }

    /// <summary>multitool_act: служебная строка спутника.</summary>
    private void OnSatelliteInteractUsing(Entity<MeteorShieldSatelliteComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, PulsingQuality))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("meteor-shield-multitool",
            ("id", ent.Comp.Id),
            ("mode", ent.Comp.Active ? "PRIMARY" : "STANDBY"),
            ("debug", IsEmagged(ent) ? "DEBUG_MODE //" : "")), ent, args.User);
    }

    private void OnSatelliteShutdown(Entity<MeteorShieldSatelliteComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.SignalEntity is { } signal)
            QueueDel(signal);

        if (ent.Comp.Active && IsEmagged(ent))
        {
            ent.Comp.Active = false;
            UpdateEmaggedSatellite(ent, null);
        }

        ent.Comp.Active = false;
        UpdateCoverage();
    }

    #endregion

    #region Емаг

    private void OnSatelliteEmagged(Entity<MeteorShieldSatelliteComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (IsEmagged(ent))
        {
            _popup.PopupEntity(Loc.GetString("meteor-shield-already-emagged"), ent, args.UserUid);
            return;
        }

        var now = _timing.CurTime;
        if (now < _emagCooldownEnd)
        {
            _popup.PopupEntity(Loc.GetString("meteor-shield-emag-cooldown",
                ("seconds", (int) Math.Ceiling((_emagCooldownEnd - now).TotalSeconds))), ent, args.UserUid, PopupType.MediumCaution);
            return;
        }

        _emagCooldownEnd = now + EmagCooldown;
        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("meteor-shield-emagged"), ent, args.UserUid, PopupType.MediumCaution);
        // AddComponent(/datum/component/gps): сигнал виден как маяк.
        ent.Comp.SignalEntity = SpawnAttachedTo(ent.Comp.Signal, new EntityCoordinates(ent, Vector2.Zero));
        Say(ent, Loc.GetString("meteor-shield-say-recalibrating", ("seconds", (int) EmagCooldown.TotalSeconds)));

        if (ent.Comp.Active)
            UpdateEmaggedSatellite(ent, args.UserUid);
    }

    /// <summary>update_emagged_meteor_sat: каждый активный взломанный спутник удваивает шанс метеоров.</summary>
    private void UpdateEmaggedSatellite(Entity<MeteorShieldSatelliteComponent> ent, EntityUid? user)
    {
        if (!ent.Comp.Active)
        {
            _emaggedActive = Math.Max(0, _emaggedActive - 1);
            if (user != null)
                _popup.PopupEntity(Loc.GetString("meteor-shield-chance-halved"), ent, user.Value);
            return;
        }

        _emaggedActive++;
        if (user != null)
            _popup.PopupEntity(Loc.GetString("meteor-shield-chance-doubled"), ent, user.Value);

        if (_emaggedActive > _highestThreshold)
        {
            _highestThreshold = _emaggedActive;
            HandleNewThreshold(ent);
        }
    }

    private void HandleNewThreshold(EntityUid satellite)
    {
        switch (_highestThreshold)
        {
            case ThresholdOne:
                Say(satellite, Loc.GetString("meteor-shield-say-threshold-one"));
                break;
            case ThresholdTwo:
                Say(satellite, Loc.GetString("meteor-shield-say-threshold-two"));
                break;
            case ThresholdThree:
                Say(satellite, Loc.GetString("meteor-shield-say-threshold-three"));
                _chat.DispatchGlobalAnnouncement(Loc.GetString("meteor-shield-announce-tampering"),
                    Loc.GetString("meteor-shield-announce-tampering-sender"), colorOverride: Color.Gold);
                break;
            case ThresholdFour:
                Say(satellite, Loc.GetString("meteor-shield-say-threshold-four"));
                LaunchDarkMatteor();
                break;
        }
    }

    private void Say(EntityUid uid, string message)
    {
        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, hideChat: false);
    }

    #endregion

    #region Перехват метеоров

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MeteorShieldSatelliteComponent>();
        while (query.MoveNext(out var uid, out var sat))
        {
            if (!sat.Active || now < sat.NextCheck)
                continue;

            sat.NextCheck = now + TimeSpan.FromSeconds(0.2);
            CheckMeteors((uid, sat));
        }

        // change_meteor_chance: взломанные щиты притягивают дополнительные метеорные рои.
        if (now >= _nextMeteorRoll)
        {
            _nextMeteorRoll = now + MeteorRollInterval;
            if (_emaggedActive > 0 && _random.Prob(Math.Min(1f, 0.05f * MathF.Pow(2, _emaggedActive))))
                _gameTicker.StartGameRule(_random.Pick(MeteorRules));
        }
    }

    /// <summary>HasProximity: метеор в радиусе и между ними только космос — луч и уничтожение.</summary>
    private void CheckMeteors(Entity<MeteorShieldSatelliteComponent> sat)
    {
        var satCoords = _xform.GetMapCoordinates(sat);
        foreach (var meteor in _lookup.GetEntitiesInRange<MeteorComponent>(satCoords, sat.Comp.KillRange))
        {
            if (TerminatingOrDeleted(meteor))
                continue;

            var meteorCoords = _xform.GetMapCoordinates(meteor);
            if (!SpaceLineOfSight(satCoords, meteorCoords))
                continue;

            SpawnBeam(sat.Comp.Beam, satCoords, meteorCoords);
            _audio.PlayPvs(sat.Comp.BeamSound, sat);

            if (HasComp<DarkMatteorComponent>(meteor))
            {
                // dark_matteor/shield_defense: луч отражается и уничтожает спутник.
                _popup.PopupCoordinates(Loc.GetString("meteor-shield-beam-reflected", ("satellite", sat.Owner)),
                    Transform(sat).Coordinates, PopupType.LargeCaution);
                Spawn("ExplosionLight", satCoords);
                QueueDel(sat);
                return;
            }

            QueueDel(meteor);
        }
    }

    /// <summary>space_los: каждая клетка на линии — космос.</summary>
    private bool SpaceLineOfSight(MapCoordinates from, MapCoordinates to)
    {
        var delta = to.Position - from.Position;
        var steps = (int) Math.Ceiling(delta.Length() * 2);
        for (var i = 0; i <= steps; i++)
        {
            var point = new MapCoordinates(from.Position + delta * (steps == 0 ? 0 : i / (float) steps), from.MapId);
            if (!_mapManager.TryFindGridAt(point, out var grid, out var gridComp))
                continue;

            var tile = _map.GetTileRef(grid, gridComp, _map.TileIndicesFor(grid, gridComp, point));
            if (!tile.Tile.IsEmpty)
                return false;
        }

        return true;
    }

    /// <summary>Beam(icon_state = "sat_beam", time = 5): цепочка сегментов на полсекунды.</summary>
    private void SpawnBeam(EntProtoId proto, MapCoordinates from, MapCoordinates to)
    {
        var delta = to.Position - from.Position;
        var length = delta.Length();
        if (length < 0.01f)
            return;

        var dir = delta / length;
        var angle = dir.ToWorldAngle();
        for (var d = 0.5f; d < length; d += 1f)
        {
            var segment = Spawn(proto, new MapCoordinates(from.Position + dir * d, from.MapId));
            _xform.SetWorldRotation(segment, angle);
        }
    }

    #endregion

    #region Покрытие и консоль

    /// <summary>update_coverage: объединение клеток в радиусе kill_range всех активных спутников станции.</summary>
    private void UpdateCoverage()
    {
        var stationMaps = new HashSet<MapId>();
        foreach (var station in _station.GetStations())
        {
            if (_station.GetLargestGrid(station) is { } grid)
                stationMaps.Add(Transform(grid).MapID);
        }

        var covered = new HashSet<(MapId, int, int)>();
        var query = EntityQueryEnumerator<MeteorShieldSatelliteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sat, out var xform))
        {
            if (!sat.Active || TerminatingOrDeleted(uid) || !stationMaps.Contains(xform.MapID))
                continue;

            var pos = _xform.GetWorldPosition(xform);
            var cx = (int) Math.Floor(pos.X);
            var cy = (int) Math.Floor(pos.Y);
            for (var x = -sat.KillRange; x <= sat.KillRange; x++)
            {
                for (var y = -sat.KillRange; y <= sat.KillRange; y++)
                {
                    covered.Add((xform.MapID, cx + x, cy + y));
                }
            }
        }

        _cachedCoverage = covered.Count;
    }

    private void OnConsoleToggle(Entity<SatelliteControlComponent> ent, ref SatelliteToggleMessage args)
    {
        var consoleMap = Transform(ent).MapID;
        var query = EntityQueryEnumerator<MeteorShieldSatelliteComponent>();
        while (query.MoveNext(out var uid, out var sat))
        {
            if (sat.Id != args.Id)
                continue;

            if (IsEmagged(uid))
            {
                _popup.PopupEntity(Loc.GetString("meteor-shield-not-responding"), ent, args.Actor);
                return;
            }

            if (Transform(uid).MapID == consoleMap)
                Toggle((uid, sat), null);

            break;
        }

        UpdateUi(ent);
    }

    private void UpdateAllConsoles()
    {
        var query = EntityQueryEnumerator<SatelliteControlComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            UpdateUi(uid);
        }
    }

    private void UpdateUi(EntityUid console)
    {
        var satellites = new List<SatelliteState>();
        var query = EntityQueryEnumerator<MeteorShieldSatelliteComponent>();
        while (query.MoveNext(out _, out var sat))
        {
            satellites.Add(new SatelliteState(sat.Id, sat.Active, sat.Mode));
        }

        satellites.Sort((a, b) => a.Id.CompareTo(b.Id));
        _ui.SetUiState(console, SatelliteControlUiKey.Key,
            new SatelliteControlUiState(satellites, true, _cachedCoverage, CoverageGoal));
    }

    #endregion

    #region Тёмно-материальный метеор

    /// <summary>dark_matteor: красный код и метеор, роняющий сингулярность.</summary>
    private void LaunchDarkMatteor()
    {
        var stations = _station.GetStations();
        if (stations.Count == 0)
            return;

        var station = _random.Pick(stations);
        if (_station.GetLargestGrid(station) is not { } grid)
            return;

        var area = _physics.GetWorldAABB(grid);
        var distance = (area.TopRight - area.Center).Length() + 50f;
        var angle = _random.NextAngle();
        var offset = angle.RotateVec(new Vector2(distance, 0));
        var spawn = new MapCoordinates(area.Center + offset, Transform(grid).MapID);

        var meteor = Spawn(DarkMatteor, spawn);
        var comp = EnsureComp<DarkMatteorComponent>(meteor);
        comp.Station = station;

        var level = _alertLevel.GetLevel(station);
        if (level is "green" or "blue" or "yellow" or "violet")
        {
            comp.PreviousLevel = level;
            _alertLevel.SetLevel(station, "red", true, true, true);
        }

        _chat.DispatchGlobalAnnouncement(Loc.GetString("meteor-shield-dark-matteor-announce"),
            Loc.GetString("meteor-shield-dark-matteor-sender"), announcementSound: comp.AnnouncementSound, colorOverride: Color.Red);

        if (TryComp<PhysicsComponent>(meteor, out var physics))
            _physics.ApplyLinearImpulse(meteor, -offset.Normalized() * 10f * physics.Mass, body: physics);
    }

    private void OnDarkMatteorDestroyed(Entity<DarkMatteorComponent> ent, ref DestructionEventArgs args)
    {
        ent.Comp.Hit = true;

        // meteor_effect: тяжёлый метеор слышен всем на карте.
        _audio.PlayGlobal(ent.Comp.Sound, Filter.BroadcastMap(Transform(ent).MapID), true, AudioParams.Default.WithVolume(5f));
    }

    /// <summary>Bump: звук при каждом ударе.</summary>
    private void OnDarkMatteorCollide(Entity<DarkMatteorComponent> ent, ref StartCollideEvent args)
    {
        if (_timing.CurTime < ent.Comp.NextImpactSound)
            return;

        ent.Comp.NextImpactSound = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        _audio.PlayPvs(ent.Comp.Sound, ent);
    }

    /// <summary>moved_off_z: промах — возвращаем прежний код и благодарим священника.</summary>
    private void OnDarkMatteorShutdown(Entity<DarkMatteorComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Hit)
            return;

        if (ent.Comp.Station is { } station && ent.Comp.PreviousLevel is { } previous && !TerminatingOrDeleted(station)
            && _alertLevel.GetLevel(station) != "delta")
        {
            _alertLevel.SetLevel(station, previous, true, true, true);
        }

        _chat.DispatchGlobalAnnouncement(Loc.GetString("meteor-shield-dark-matteor-missed"),
            Loc.GetString("meteor-shield-dark-matteor-sender"));
    }

    #endregion
}
