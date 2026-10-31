using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Camera;
using Content.Shared.Imperial.Cadia;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Падение завесы пилонов — главный момент начала боя. За полминуты до конца подготовки над Касром воет воздушная тревога,
/// а флагман Хаоса начинает хорал. Когда завеса падает, пилоны вспыхивают и гаснут, земля дрожит, небо на миг краснеет,
/// и первый залп флота Абаддона ложится на восточную пустошь.
/// </summary>
public sealed class CadiaVeilFallSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;
    [Dependency] private readonly CadiaRefugeeSystem _refugees = default!;

    private static readonly TimeSpan Warning = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckRate = TimeSpan.FromSeconds(0.5);
    private static readonly SoundSpecifier SirenSound = new SoundPathSpecifier("/Audio/Imperial/MeteorShield/airraid.ogg");
    private static readonly SoundSpecifier ChantSound = new SoundPathSpecifier("/Audio/Imperial/Cadia/Ambience/rift_drone.ogg");
    private static readonly SoundSpecifier PylonSound = new SoundPathSpecifier("/Audio/Magic/forcewall.ogg", AudioParams.Default.WithVolume(6).WithMaxDistance(40));
    private static readonly SoundSpecifier ThunderSound = new SoundPathSpecifier("/Audio/Magic/rumble.ogg", AudioParams.Default.WithVolume(4));

    /// <summary>Восточная пустошь в мировых координатах поля (240×150, начало в центре): сюда ложится первый залп.</summary>
    private static readonly Box2 Wasteland = new(72f, -62f, 112f, 62f);
    private const int Salvo = 7;

    private bool _warned;
    private bool _wasUp;
    private TimeSpan _next;
    private readonly List<(TimeSpan At, MapCoordinates Where)> _shells = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        for (var i = _shells.Count - 1; i >= 0; i--)
        {
            if (now < _shells[i].At)
                continue;
            _explosion.QueueExplosion(_shells[i].Where, "Default", 45f, 4f, 9f, null, canCreateVacuum: false);
            _shells.RemoveAt(i);
        }

        if (now < _next)
            return;
        _next = now + CheckRate;

        if (!_rule.TryGetActiveWar(out var war))
        {
            _warned = false;
            _wasUp = false;
            return;
        }

        var up = CadiaPylonVeilSystem.VeilUp(war);
        if (up && war.VeilOverride == null && war.Phase == CadiaWarPhase.Preparation && !_warned && war.PhaseEnd - now <= Warning)
        {
            _warned = true;
            _rule.Announce(CadiaFaction.Imperium, Loc.GetString("cadia-veil-siren-imperium"), SirenSound);
            _rule.Announce(CadiaFaction.Chaos, Loc.GetString("cadia-veil-siren-chaos"), ChantSound);
        }
        if (up)
            _warned &= war.Phase == CadiaWarPhase.Preparation;
        if (_wasUp && !up)
            Fall();
        _wasUp = up;
    }

    private void Fall()
    {
        var now = _timing.CurTime;
        MapId? field = null;

        // Пилоны вспыхивают и гаснут.
        var pylons = EntityQueryEnumerator<CadiaPylonComponent, TransformComponent>();
        while (pylons.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid == null)
                continue;
            field ??= xform.MapID;
            Spawn("CadiaVeilBurst", _xform.GetMapCoordinates(uid, xform));
            _audio.PlayPvs(PylonSound, uid);
        }

        // Земля дрожит у всех бойцов.
        var mobs = EntityQueryEnumerator<CadiaFactionMemberComponent>();
        while (mobs.MoveNext(out var mob, out var member))
        {
            if (member.Role == null)
                continue;
            _recoil.KickCamera(mob, _random.NextVector2(2f, 3.5f));
        }
        _audio.PlayGlobal(ThunderSound, Robust.Shared.Player.Filter.Broadcast(), true);
        RaiseNetworkEvent(new CadiaVeilFallEvent());

        // Первый залп флота — по пустоши, где ещё никого нет.
        if (field is { } map)
        {
            for (var i = 0; i < Salvo; i++)
            {
                var at = new Vector2(_random.NextFloat(Wasteland.Left, Wasteland.Right), _random.NextFloat(Wasteland.Bottom, Wasteland.Top));
                _shells.Add((now + TimeSpan.FromSeconds(2.5 + i * 0.6 + _random.NextDouble() * 0.4), new MapCoordinates(at, map)));
            }
        }

        _rule.Feed(Loc.GetString("cadia-veil-fallen-feed"), CadiaFaction.Chaos);
        _refugees.ReportOnFall();
    }
}
