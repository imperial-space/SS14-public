using Content.Server.Popups;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Варп-штормы со штурма и до конца войны: кровавый дождь и шёпот бьют по морали защитников под открытым небом,
/// вспышка демонов открывает малые разломы у пилонов и секторов.
/// </summary>
public sealed class CadiaWarpStormSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly CadiaMoraleSystem _morale = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;

    private static readonly EntProtoId[] SurgeDaemons = { "CadiaMobBloodletter", "CadiaMobPlaguebearer", "CadiaMobDaemonette" };
    private static readonly EntProtoId SurgeRift = "CadiaWarpRiftSmall";
    private static readonly SoundSpecifier StormSound = new SoundPathSpecifier("/Audio/Imperial/DeadSector/Sound/Ambient/Blowout/blowoutambientrumble.ogg");

    private const int WhisperCount = 6;
    private const int SurgeWarp = 15;
    private const int SurgeRifts = 3;
    private const float RainFear = -1f;
    private const float RainZeal = 0.5f;
    private const float WhisperFear = -5f;
    private const float WhisperChance = 0.5f;
    private static readonly TimeSpan MinPause = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxPause = TimeSpan.FromMinutes(8);

    private TimeSpan _next;
    private TimeSpan _nextStorm;
    private TimeSpan _nextWhisper;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _next)
            return;
        _next = now + TimeSpan.FromSeconds(1);

        if (!_rule.TryGetActiveWar(out var war) || war.Phase < CadiaWarPhase.Assault || war.Phase == CadiaWarPhase.Ended)
        {
            _nextStorm = TimeSpan.Zero;
            return;
        }

        if (war.Storm != CadiaWarpStorm.None)
        {
            if (now >= war.StormEnd)
            {
                war.Storm = CadiaWarpStorm.None;
                _rule.DirtyWar();
                return;
            }
            ApplyStorm(war.Storm, now);
            return;
        }

        if (_nextStorm == TimeSpan.Zero)
            _nextStorm = now + _random.Next(MinPause, MaxPause);
        if (now >= _nextStorm)
            Start(_random.Pick(new[] { CadiaWarpStorm.BloodRain, CadiaWarpStorm.Whispers, CadiaWarpStorm.DaemonSurge }));
    }

    /// <summary>Начать шторм сейчас: по таймеру или по кнопке ведущего.</summary>
    public void Start(CadiaWarpStorm storm)
    {
        if (!_rule.TryGetActiveWar(out var war) || war.Phase == CadiaWarPhase.Ended)
            return;
        var now = _timing.CurTime;
        var length = TimeSpan.FromSeconds(storm == CadiaWarpStorm.DaemonSurge ? 20 : _random.Next(40, 60));
        war.Storm = storm;
        war.StormEnd = now + length;
        _rule.DirtyWar();
        _nextStorm = now + length + _random.Next(MinPause, MaxPause);
        _nextWhisper = now;

        var key = storm.ToString().ToLowerInvariant();
        _rule.Announce(CadiaFaction.Imperium, Loc.GetString($"cadia-storm-{key}-imperium"), StormSound);
        _rule.Announce(CadiaFaction.Chaos, Loc.GetString($"cadia-storm-{key}-chaos"), StormSound);
        _rule.Feed(Loc.GetString($"cadia-feed-storm-{key}"), CadiaFaction.Chaos);

        if (storm == CadiaWarpStorm.DaemonSurge)
            Surge();
    }

    private void Surge()
    {
        _rule.AddWarp(SurgeWarp);
        var spots = new List<MapCoordinates>();
        var pylons = EntityQueryEnumerator<CadiaPylonComponent>();
        while (pylons.MoveNext(out var uid, out _))
            spots.Add(_xform.GetMapCoordinates(uid));
        var sectors = EntityQueryEnumerator<CadiaSectorComponent>();
        while (sectors.MoveNext(out var uid, out _))
            spots.Add(_xform.GetMapCoordinates(uid));
        if (spots.Count == 0)
            return;
        for (var i = 0; i < SurgeRifts; i++)
        {
            var at = _random.Pick(spots).Offset(_random.NextVector2(3f, 7f));
            Spawn(SurgeRift, at);
            Spawn(_random.Pick(SurgeDaemons), at);
        }
    }

    private void ApplyStorm(CadiaWarpStorm storm, TimeSpan now)
    {
        if (storm == CadiaWarpStorm.DaemonSurge)
            return;
        var whisperNow = storm == CadiaWarpStorm.Whispers && now >= _nextWhisper;
        if (whisperNow)
            _nextWhisper = now + TimeSpan.FromSeconds(6);

        var query = EntityQueryEnumerator<CadiaMoraleComponent, CadiaFactionMemberComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var member, out var xform))
        {
            if (member.Role == null || !_mobState.IsAlive(uid) || HasComp<CadiaAstartesComponent>(uid) || !UnderSky(xform))
                continue;

            if (storm == CadiaWarpStorm.BloodRain)
            {
                _morale.Change(uid, member.Faction == CadiaFaction.Imperium ? RainFear : RainZeal);
                continue;
            }
            if (whisperNow && member.Faction == CadiaFaction.Imperium && _random.Prob(WhisperChance))
            {
                _morale.Change(uid, WhisperFear);
                _popup.PopupEntity(Loc.GetString($"cadia-whisper-{_random.Next(1, WhisperCount + 1)}"), uid, uid, PopupType.MediumCaution);
            }
        }
    }

    /// <summary>В кабине техники и на орбите шторм не достаёт.</summary>
    private bool UnderSky(TransformComponent xform)
    {
        if (xform.MapUid is not { } map || HasComp<CadiaVehicleInteriorComponent>(map))
            return false;
        return CompOrNull<CadiaFlightLevelComponent>(map)?.Level != CadiaFlightLevel.Orbit;
    }
}
