using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Audio.Components;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

/// <summary>
/// Звук войны: у каждого свой фон по фазе (гул, бой на горизонте) и далёкие взрывы и очереди, которые слышно с той стороны,
/// где идёт бой. На открытом поле у них эхо гор, в зданиях — каменной комнаты, в катакомбах — пещеры, и там они глуше.
/// На орбите тишина, в небе и в кабинах приглушено. Громкость следует ползунку фоновых звуков. И шкала морали.
/// </summary>
public sealed class CadiaBattleClientSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedRoofSystem _roof = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly Content.Shared.Damage.Systems.DamageableSystem _damageable = default!;

    // ─── Музыка жёсткого боя ───
    // Накал считается каждые четверть секунды: враги рядом, снаряды вокруг, полученный урон. Выше порога — вступает
    // боевая тема, а музыка фазы уходит под неё; бой стих на несколько секунд — всё плавно возвращается.
    private const string CombatDir = "/Audio/Imperial/Cadia/Music/Combat/";
    private const float EnemyRange = 13f;
    private const float ProjectileRange = 9f;
    private const float HeatEnter = 0.55f;
    private const float HeatExit = 0.18f;
    private const float HeatDecayPerSecond = 0.12f;
    private const float FadeSeconds = 2.5f;
    private const float QuietDb = -28f;
    private static readonly TimeSpan CalmBeforeExit = TimeSpan.FromSeconds(9);
    private static readonly TimeSpan HeatTick = TimeSpan.FromSeconds(0.25);

    private EntityUid? _combatMusic;
    private float _heat;
    private float _mix;
    private bool _inCombat;
    private TimeSpan _calmSince;
    private TimeSpan _nextHeat;
    private FixedPoint2 _lastDamage;
    private readonly HashSet<Entity<CadiaFactionMemberComponent>> _near = new();
    private readonly HashSet<Entity<ProjectileComponent>> _shots = new();

    private const string Dir = "/Audio/Imperial/Cadia/Ambience/";
    private static readonly string[] FarBlasts = { "far_blast1.ogg", "far_blast2.ogg", "far_blast3.ogg", "far_blast4.ogg", "artillery.ogg" };
    private static readonly string[] FarGuns = { "far_gun1.ogg", "far_gun2.ogg", "far_firefight.ogg" };
    private const string BattleLoop = "battle_loop.ogg";
    private const string DroneLoop = "war_drone.ogg";
    private const string VoxStatic = "vox_static.ogg";

    private const string MusicDir = "/Audio/Imperial/Cadia/Music/";
    private const float MusicBaseVolume = -10f;
    private EntityUid? _music;
    private EntityUid? _battle;
    private EntityUid? _drone;
    private CadiaWarPhase? _phase;
    private float _muffle;
    private string? _acoustics;
    private readonly Dictionary<string, EntityUid> _aux = new();

    // Фронт на поле Кадии лежит к востоку от Касра: западнее этой линии бой слышно с востока, восточнее — с запада.
    private const float FrontX = 30f;
    private const float FarMin = 22f;
    private const float FarMax = 42f;
    private const float NearMin = 7f;
    private const float NearMax = 16f;
    private TimeSpan _nextCheck;
    private TimeSpan _nextShot;
    private CadiaMoraleHudOverlay? _hud;

    public override void Initialize()
    {
        base.Initialize();
        _hud = new CadiaMoraleHudOverlay();
        _overlays.AddOverlay(_hud);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_hud != null)
            _overlays.RemoveOverlay(_hud);
        StopLoops();
        foreach (var aux in _aux.Values)
            QueueDel(aux);
        _aux.Clear();
    }

    /// <summary>Эффект реверберации по пресету движка: создаётся один раз и переиспользуется всеми звуками.</summary>
    private EntityUid? Aux(string? preset)
    {
        if (preset == null || !_proto.TryIndex<AudioPresetPrototype>(preset, out var proto))
            return null;
        if (_aux.TryGetValue(preset, out var existing) && !TerminatingOrDeleted(existing))
            return existing;
        var aux = _audio.CreateAuxiliary();
        var effect = _audio.CreateEffect();
        _audio.SetEffectPreset(effect.Entity, effect.Component, proto);
        _audio.SetEffect(aux.Entity, aux.Component, effect.Entity);
        _aux[preset] = aux.Entity;
        return aux.Entity;
    }

    private void ApplyAcoustics((EntityUid Entity, AudioComponent Component)? played)
    {
        if (played is { } sound && Aux(_acoustics) is { } aux)
            _audio.SetAuxiliary(sound.Entity, sound.Component, aux);
    }

    /// <summary>Где стоит слушатель: под землёй, под крышей или под открытым небом.</summary>
    private (float Muffle, string? Acoustics) Surroundings(EntityUid local, EntityUid? map, bool inCabin, CadiaFlightLevel? level)
    {
        if (inCabin)
            return (-8f, null);
        if (level == CadiaFlightLevel.Air)
            return (-5f, null);
        if (CompOrNull<ZLevelMapComponent>(map)?.Level < 0)
            return (-12f, "Cave");
        var xform = Transform(local);
        if (xform.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var gridComp) && TryComp<RoofComponent>(grid, out var roof) &&
            _roof.IsRooved((grid, gridComp, roof), _map.TileIndicesFor(grid, gridComp, xform.Coordinates)))
            return (-5f, "StoneRoom");
        return (0f, "Mountains");
    }

    private void StopLoops()
    {
        _combatMusic = _audio.Stop(_combatMusic);
        _inCombat = false;
        _mix = 0f;
        _heat = 0f;
        _music = _audio.Stop(_music);
        _battle = _audio.Stop(_battle);
        _drone = _audio.Stop(_drone);
        _phase = null;
    }

    private float MusicVolume()
    {
        return MusicBaseVolume + SharedAudioSystem.GainToVolume(MathF.Max(0.001f, _cfg.GetCVar(CCVars.AmbientMusicVolume))) + _muffle;
    }

    private float AmbienceVolume()
    {
        return SharedAudioSystem.GainToVolume(MathF.Max(0.001f, _cfg.GetCVar(CCVars.AmbienceVolume)));
    }

    private EntityUid? StartLoop(string file, float volume)
    {
        var played = _audio.PlayGlobal(new SoundPathSpecifier(Dir + file), Filter.Local(), false,
            AudioParams.Default.WithLoop(true).WithVolume(volume + AmbienceVolume() + _muffle));
        ApplyAcoustics(played);
        return played?.Entity;
    }

    /// <summary>Какая боевая тема: Империум в обороне, Хаос в натиске, а в развязке и при падении Кадии у всех одна.</summary>
    private string CombatTrack(CadiaWarStateComponent war, EntityUid local)
    {
        if (war.Phase >= CadiaWarPhase.Finale)
            return "finale.ogg";
        var side = CompOrNull<CadiaFactionMemberComponent>(local)?.Faction;
        return side == CadiaFaction.Chaos ? "chaos.ogg" : "imperium.ogg";
    }

    private void UpdateCombat(float frameTime, CadiaWarStateComponent? war, EntityUid? local)
    {
        var now = _timing.RealTime;
        if (war != null && local is { } me && now >= _nextHeat)
        {
            _nextHeat = now + HeatTick;
            var instant = 0f;
            var mine = CompOrNull<CadiaFactionMemberComponent>(me)?.Faction;
            var coords = Transform(me).Coordinates;
            _near.Clear();
            _lookup.GetEntitiesInRange(coords, EnemyRange, _near);
            foreach (var other in _near)
            {
                if (other.Owner != me && other.Comp.Role != null && other.Comp.Faction != mine && _mobState.IsAlive(other))
                    instant += 0.14f;
            }
            instant = MathF.Min(instant, 0.6f);
            _shots.Clear();
            _lookup.GetEntitiesInRange(coords, ProjectileRange, _shots);
            instant += MathF.Min(_shots.Count * 0.08f, 0.45f);
            if (TryComp<DamageableComponent>(me, out var damage))
            {
                // Только знак: ранили ли сейчас. Сколько именно — неважно, поэтому общий счёт урона здесь уместен.
#pragma warning disable CS0618
                var total = _damageable.GetTotalDamage((me, damage));
#pragma warning restore CS0618
                if (total > _lastDamage + 2)
                    instant += 0.6f;
                _lastDamage = total;
            }
            _heat = MathF.Max(_heat - HeatDecayPerSecond * (float) HeatTick.TotalSeconds, MathF.Min(instant, 1f));

            if (!_inCombat && _heat >= HeatEnter && _music != null)
            {
                _inCombat = true;
                _combatMusic = _audio.Stop(_combatMusic);
                _combatMusic = _audio.PlayGlobal(new SoundPathSpecifier(CombatDir + CombatTrack(war, me)), Filter.Local(), false,
                    AudioParams.Default.WithLoop(true).WithVolume(MusicVolume() + QuietDb))?.Entity;
            }
            if (_heat > HeatExit)
                _calmSince = now;
            else if (_inCombat && now - _calmSince > CalmBeforeExit)
                _inCombat = false;
        }

        // Плавный переход: громкость меняется каждый кадр, без рывков.
        var target = _inCombat ? 1f : 0f;
        if (MathHelper.CloseTo(_mix, target) && (!(_mix <= 0f) || _combatMusic == null))
            return;
        _mix = target > _mix ? MathF.Min(target, _mix + frameTime / FadeSeconds) : MathF.Max(target, _mix - frameTime / FadeSeconds);
        _audio.SetVolume(_music, MusicVolume() + QuietDb * _mix);
        _audio.SetVolume(_combatMusic, MusicVolume() + QuietDb * (1f - _mix));
        if (_mix <= 0f)
            _combatMusic = _audio.Stop(_combatMusic);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var now = _timing.RealTime;
        CadiaWarStateComponent? liveWar = null;
        var liveWars = EntityQueryEnumerator<CadiaWarStateComponent>();
        while (liveWars.MoveNext(out var liveState))
            liveWar = liveState;
        UpdateCombat(frameTime, liveWar is { Phase: not CadiaWarPhase.Ended } ? liveWar : null, _player.LocalEntity);
        if (now < _nextCheck)
            return;
        _nextCheck = now + TimeSpan.FromSeconds(1);

        // Состояние войны есть только во время ивента, его видят все клиенты.
        CadiaWarStateComponent? war = null;
        var wars = EntityQueryEnumerator<CadiaWarStateComponent>();
        while (wars.MoveNext(out var state))
            war = state;

        if (war == null || war.Phase == CadiaWarPhase.Ended || _player.LocalEntity is not { } local)
        {
            StopLoops();
            return;
        }

        var map = Transform(local).MapUid;
        var level = CompOrNull<CadiaFlightLevelComponent>(map)?.Level;
        var inCabin = HasComp<CadiaVehicleInteriorComponent>(map);
        if (level == CadiaFlightLevel.Orbit)
        {
            StopLoops();
            return;
        }

        var (muffle, acoustics) = Surroundings(local, map, inCabin, level);
        if (_phase != war.Phase || Math.Abs(muffle - _muffle) > 0.1f || acoustics != _acoustics)
        {
            _acoustics = acoustics;
            // Фон боя глохнет заново, а музыка при входе в кабину играет дальше: начинается сначала только с новой фазой.
            var phaseChanged = _phase != war.Phase;
            _battle = _audio.Stop(_battle);
            _drone = _audio.Stop(_drone);
            _muffle = muffle;
            _phase = war.Phase;
            switch (war.Phase)
            {
                case CadiaWarPhase.Preparation:
                    _drone = StartLoop(DroneLoop, -8f);
                    break;
                case CadiaWarPhase.Bombardment:
                    _drone = StartLoop(DroneLoop, -4f);
                    break;
                case CadiaWarPhase.Assault:
                    _battle = StartLoop(BattleLoop, -14f);
                    break;
                case CadiaWarPhase.Finale:
                case CadiaWarPhase.Evacuation:
                    _battle = StartLoop(BattleLoop, -10f);
                    _drone = StartLoop(DroneLoop, -6f);
                    break;
            }
            if (phaseChanged)
            {
                var track = war.Phase switch
                {
                    CadiaWarPhase.Preparation => "preparation.ogg",
                    CadiaWarPhase.Bombardment => "bombardment.ogg",
                    CadiaWarPhase.Assault => "assault.ogg",
                    // Победа Империума звучит торжественно, падение Кадии остаётся мрачным.
                    CadiaWarPhase.Ended when war.Winner == CadiaFaction.Imperium => "assault.ogg",
                    _ => "finale.ogg",
                };
                _music = _audio.Stop(_music);
                _music = _audio.PlayGlobal(new SoundPathSpecifier(MusicDir + track), Filter.Local(), false,
                    AudioParams.Default.WithLoop(true).WithVolume(MusicVolume()))?.Entity;
            }
            else
            {
                _audio.SetVolume(_music, MusicVolume() + QuietDb * _mix);
            }
            _nextShot = now + TimeSpan.FromSeconds(_random.NextFloat(3f, 8f));
        }

        if (now < _nextShot)
            return;
        var (min, max) = war.Phase switch
        {
            CadiaWarPhase.Preparation => (35f, 80f),
            CadiaWarPhase.Bombardment => (5f, 14f),
            CadiaWarPhase.Assault => (6f, 16f),
            _ => (4f, 10f),
        };
        _nextShot = now + TimeSpan.FromSeconds(_random.NextFloat(min, max));
        PlayOneShot(war.Phase, local, map);
    }

    private void PlayOneShot(CadiaWarPhase phase, EntityUid local, EntityUid? map)
    {
        string file;
        var volume = _random.NextFloat(-16f, -8f);
        if (_random.Prob(0.08f))
        {
            file = VoxStatic;
            volume = -18f;
        }
        else if (phase == CadiaWarPhase.Bombardment || _random.Prob(0.5f))
        {
            file = _random.Pick(FarBlasts);
        }
        else
        {
            file = _random.Pick(FarGuns);
        }
        // Вокс шипит в голове, а бой звучит с места: так слышно, где фронт и куда ложатся снаряды.
        if (file == VoxStatic || map is not { } mapUid)
        {
            _audio.PlayGlobal(new SoundPathSpecifier(Dir + file), Filter.Local(), false,
                AudioParams.Default.WithVolume(volume + AmbienceVolume() + _muffle).WithVariation(0.08f));
            return;
        }

        var here = _xform.GetWorldPosition(local);
        float distance;
        float angle;
        if (phase == CadiaWarPhase.Bombardment && FarBlasts.Contains(file) && _random.Prob(0.4f))
        {
            // Обстрел: снаряды рвутся вокруг, с любой стороны.
            distance = _random.NextFloat(NearMin, NearMax);
            angle = _random.NextFloat(0f, MathF.Tau);
        }
        else
        {
            distance = _random.NextFloat(FarMin, FarMax);
            angle = (here.X < FrontX ? 0f : MathF.PI) + _random.NextFloat(-1.2f, 1.2f);
        }
        var at = here + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        var played = _audio.PlayStatic(new SoundPathSpecifier(Dir + file), Filter.Local(), new EntityCoordinates(mapUid, at), false,
            AudioParams.Default.WithVolume(volume + 8f + AmbienceVolume() + _muffle).WithVariation(0.08f)
                .WithMaxDistance(FarMax + 30f).WithReferenceDistance(10f).WithRolloffFactor(0.5f));
        ApplyAcoustics(played);
    }
}

/// <summary>Шкала морали в верхнем левом углу: космодесант страха не ведает и шкалы не видит.</summary>
public sealed class CadiaMoraleHudOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color VeilColor = Color.FromHex("#5affc8");
    private static readonly Color VeilEnding = Color.FromHex("#ff5a3a");
    /// <summary>В последние секунды отсчёт краснеет — сейчас начнётся обстрел.</summary>
    private static readonly TimeSpan VeilWarning = TimeSpan.FromSeconds(30);

    private static readonly Color Steady = Color.FromHex("#5aff78");
    private static readonly Color Shaken = Color.FromHex("#ffd37a");
    private static readonly Color Broken = Color.FromHex("#ff3a2a");
    private const float BarWidth = 470f;

    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaMoraleHudOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 11);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        DrawMorale(args);
        DrawVeil(args.ScreenHandle, args.ViewportBounds);
    }

    private void DrawMorale(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local ||
            !_entMan.TryGetComponent<CadiaMoraleComponent>(local, out var morale) ||
            _entMan.HasComponent<CadiaAstartesComponent>(local) ||
            !_entMan.TryGetComponent<CadiaFactionMemberComponent>(local, out var member) || member.Role == null)
            return;

        var value = morale.Morale;
        var color = value < CadiaMoraleComponent.BrokenBelow ? Broken : value < CadiaMoraleComponent.ShakenBelow ? Shaken : Steady;
        var key = value < CadiaMoraleComponent.BrokenBelow ? "cadia-morale-hud-broken" : value < CadiaMoraleComponent.ShakenBelow ? "cadia-morale-hud-shaken" : "cadia-morale-hud-steady";

        var handle = args.ScreenHandle;
        // Под панелью войны: левый верхний угол закрывает панель действий.
        var at = new Vector2(args.ViewportBounds.Left + (args.ViewportBounds.Width - BarWidth) / 2f, args.ViewportBounds.Top + 106);
        var box = UIBox2.FromDimensions(at, new Vector2(BarWidth, 17));
        handle.DrawRect(box, Color.FromHex("#0b0d0ad8"));
        handle.DrawRect(UIBox2.FromDimensions(at, new Vector2(BarWidth * Math.Clamp(value / 100f, 0f, 1f), 17)), color.WithAlpha(0.45f));
        handle.DrawRect(box, color.WithAlpha(0.7f), false);
        var text = Loc.GetString(key, ("value", (int) value));
        handle.DrawString(_font, at + new Vector2(6, 2), text, Color.Black);
        handle.DrawString(_font, at + new Vector2(5, 1), text, Color.White);
    }

    /// <summary>
    /// У правого края шкалы морали: сколько ещё держится завеса пилонов (или что её держит ведущий). Ниже шкалы — строка
    /// цели, поэтому таймер живёт в той же строке, что и мораль; у Астартес без шкалы — своя подложка на её месте.
    /// </summary>
    private void DrawVeil(DrawingHandleScreen handle, UIBox2i bounds)
    {
        if (_player.LocalEntity is not { } local ||
            !_entMan.TryGetComponent<CadiaFactionMemberComponent>(local, out var member) || member.Role == null)
            return;
        var wars = _entMan.EntityQueryEnumerator<CadiaWarStateComponent>();
        while (wars.MoveNext(out var war))
        {
            if (!CadiaPylonVeilSystem.VeilUp(war))
                continue;
            var color = VeilColor;
            string text;
            if (war.VeilOverride == null)
            {
                var left = war.PhaseEnd - _timing.CurTime;
                if (left < TimeSpan.Zero)
                    left = TimeSpan.Zero;
                if (left <= VeilWarning)
                    color = VeilEnding;
                text = Loc.GetString("cadia-veil-hud-timer", ("time", $"{(int) left.TotalMinutes}:{left.Seconds:00}"));
            }
            else
            {
                text = Loc.GetString("cadia-veil-hud-held");
            }
            var size = handle.GetDimensions(_font, text, 1f);
            var bar = new Vector2(bounds.Left + (bounds.Width - BarWidth) / 2f, bounds.Top + 106);
            var at = new Vector2(bar.X + BarWidth - size.X - 6, bar.Y + 1);
            if (!_entMan.HasComponent<CadiaMoraleComponent>(local) || _entMan.HasComponent<CadiaAstartesComponent>(local))
                handle.DrawRect(UIBox2.FromDimensions(at - new Vector2(6, 1), size + new Vector2(12, 2)), Color.FromHex("#0b0d0ad8"));
            handle.DrawString(_font, at + new Vector2(1, 1), text, Color.Black);
            handle.DrawString(_font, at, text, color);
            return;
        }
    }
}
