using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.Mobs;
using Robust.Shared.Random;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Горожанин Касра: бродит по улицам, пока его не уведут в убежище.</summary>
[RegisterComponent]
public sealed partial class CadiaRefugeeComponent : Component;

/// <summary>Убежище в катакомбах: горожанин, доведённый сюда, укрыт.</summary>
[RegisterComponent]
public sealed partial class CadiaRefugeeShelterComponent : Component
{
    [DataField]
    public float Radius = 6f;
}

/// <summary>
/// Беженцы Касра. Пока идёт подготовка, по улицам бродят горожане; гвардия уводит их в убежище в катакомбах — за руку
/// (тащить) по трапу вниз. Укрытый горожанин поднимает дух защитников рядом, а с падением завесы обе стороны узнают,
/// сколько людей успели спрятать. После падения завесы оставшиеся на улицах кричат от страха, а гибель горожанина
/// бьёт по духу гвардейцев, которые это видели.
/// </summary>
public sealed class CadiaRefugeeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;
    [Dependency] private readonly CadiaMoraleSystem _morale = default!;
    [Dependency] private readonly CadiaStatsSystem _stats = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly TimeSpan CheckRate = TimeSpan.FromSeconds(1);
    private static readonly SoundSpecifier SavedSound = new SoundPathSpecifier("/Audio/Imperial/Cadia/Ambience/bell_toll.ogg", AudioParams.Default.WithVolume(-8));
    /// <summary>Сколько духа получают защитники рядом с убежищем за каждого укрытого.</summary>
    private const float MoraleGain = 8f;
    private const float MoraleRange = 10f;
    /// <summary>Смерть горожанина на глазах у гвардии.</summary>
    private const float DeathMorale = -6f;
    private const float DeathRange = 8f;
    /// <summary>Как часто кто-то из оставшихся на улицах кричит в панике.</summary>
    private static readonly TimeSpan PanicMin = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PanicMax = TimeSpan.FromSeconds(12);
    private static readonly string[] PanicEmotes = { "Scream", "Scream", "Crying", "Gasp" };

    private TimeSpan _next;
    private TimeSpan _nextPanic;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaRefugeeComponent, MobStateChangedEvent>(OnMobState);
    }

    private void OnMobState(Entity<CadiaRefugeeComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !_rule.TryGetActiveWar(out _))
            return;
        _near.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, DeathRange, _near);
        foreach (var guard in _near)
        {
            if (guard.Comp.Faction == CadiaFaction.Imperium)
                _morale.Change(guard, DeathMorale);
        }
        _rule.Feed(Loc.GetString("cadia-refugee-died-feed"), CadiaFaction.Chaos);
        _stats.Count(CadiaWarCount.RefugeesDied);
    }
    private readonly HashSet<Entity<CadiaFactionMemberComponent>> _near = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _next)
            return;
        _next = now + CheckRate;
        if (!_rule.TryGetActiveWar(out var war))
            return;

        var total = war.RefugeesSaved;
        var saved = new List<(EntityUid Refugee, EntityUid Shelter)>();
        var refugees = EntityQueryEnumerator<CadiaRefugeeComponent, TransformComponent>();
        while (refugees.MoveNext(out var uid, out _, out var xform))
        {
            total++;
            if (!_mobState.IsAlive(uid))
                continue;
            var pos = _xform.GetMapCoordinates(uid, xform);
            var shelters = EntityQueryEnumerator<CadiaRefugeeShelterComponent, TransformComponent>();
            while (shelters.MoveNext(out var shelter, out var comp, out var shelterXform))
            {
                var at = _xform.GetMapCoordinates(shelter, shelterXform);
                if (at.MapId == pos.MapId && (at.Position - pos.Position).Length() <= comp.Radius)
                {
                    saved.Add((uid, shelter));
                    break;
                }
            }
        }

        if (!CadiaPylonVeilSystem.VeilUp(war) && now >= _nextPanic)
        {
            _nextPanic = now + _random.Next(PanicMin, PanicMax);
            Panic();
        }

        foreach (var (refugee, shelter) in saved)
        {
            war.RefugeesSaved++;
            _stats.Count(CadiaWarCount.RefugeesSaved);
            _popup.PopupEntity(Loc.GetString("cadia-refugee-saved", ("name", Name(refugee))), shelter, PopupType.Medium);
            _audio.PlayPvs(SavedSound, shelter);
            _near.Clear();
            _lookup.GetEntitiesInRange(Transform(shelter).Coordinates, MoraleRange, _near);
            foreach (var guard in _near)
            {
                if (guard.Comp.Faction == CadiaFaction.Imperium)
                    _morale.Change(guard, MoraleGain);
            }
            QueueDel(refugee);
        }
        if (saved.Count > 0)
            _rule.Feed(Loc.GetString("cadia-refugee-feed", ("saved", war.RefugeesSaved), ("total", total)), CadiaFaction.Imperium);
        if (war.RefugeesTotal != total || saved.Count > 0)
        {
            war.RefugeesTotal = total;
            _rule.DirtyWar();
        }
    }

    /// <summary>Один из горожан на улицах кричит в панике.</summary>
    private void Panic()
    {
        var alive = new List<EntityUid>();
        var refugees = EntityQueryEnumerator<CadiaRefugeeComponent>();
        while (refugees.MoveNext(out var uid, out _))
        {
            if (_mobState.IsAlive(uid))
                alive.Add(uid);
        }
        if (alive.Count > 0)
            _chat.TryEmoteWithChat(_random.Pick(alive), _random.Pick(PanicEmotes), ignoreActionBlocker: true);
    }

    /// <summary>Завеса пала: сколько горожан успели укрыть.</summary>
    public void ReportOnFall()
    {
        if (!_rule.TryGetActiveWar(out var war) || war.RefugeesTotal == 0)
            return;
        _rule.Broadcast(Loc.GetString("cadia-refugee-report", ("saved", war.RefugeesSaved), ("total", war.RefugeesTotal)), CadiaFaction.Imperium);
        _rule.Broadcast(Loc.GetString("cadia-refugee-report-chaos", ("left", war.RefugeesTotal - war.RefugeesSaved)), CadiaFaction.Chaos);
    }
}
