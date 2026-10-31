using System.Numerics;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Пусковая шахта когтистой капсулы на десантной палубе флагмана.</summary>
[RegisterComponent]
public sealed partial class CadiaDropBerthComponent : Component
{
    /// <summary>Капсула возвращается на корабль не сразу.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(25);

    /// <summary>Кто стоит в этом радиусе от шахты, тот и летит.</summary>
    [DataField]
    public float Radius = 1.5f;

    /// <summary>Мест в капсуле.</summary>
    [DataField]
    public int Capacity = 6;

    [ViewVariables]
    public TimeSpan NextLaunch;

    /// <summary>Сколько урона шахта выдерживает, прежде чем разбиться.</summary>
    [DataField]
    public float BreakAt = 120f;

    /// <summary>Кто умеет чинить разбитую шахту.</summary>
    [DataField]
    public List<string> Repairers = new() { "CadiaHeretek" };

    [DataField]
    public TimeSpan RepairTime = TimeSpan.FromSeconds(12);

    /// <summary>Шахта разбита: капсулы не стартуют, над ней горит пожар.</summary>
    [ViewVariables]
    public bool Broken;

    [ViewVariables]
    public EntityUid? Fire;
}

/// <summary>Зона высадки капсул на поле Кадии.</summary>
[RegisterComponent]
public sealed partial class CadiaDropZoneComponent : Component
{
    [DataField(required: true)]
    public LocId Label;
}

/// <summary>Капсула в пути: кого везёт, подбита ли и когда садится, взлетает и исчезает (только сервер).</summary>
[RegisterComponent]
public sealed partial class CadiaDropPodFlightComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Passengers = new();

    [ViewVariables]
    public TimeSpan Impact;

    [ViewVariables]
    public TimeSpan LiftOff;

    [ViewVariables]
    public TimeSpan Gone;

    [ViewVariables]
    public bool Landed;

    /// <summary>ПВО попало: посадка жёсткая и мимо зоны, отделение ранено.</summary>
    [ViewVariables]
    public bool Hit;

    /// <summary>Разрывы зениток вокруг капсулы на спуске.</summary>
    [ViewVariables]
    public List<TimeSpan> Flak = new();
}

/// <summary>
/// Высадка Хаоса когтистыми капсулами. Отделение встаёт у пусковой шахты флагмана и выбирает зону высадки; капсула падает
/// на поле, бьёт всех под собой, бойцы выходят вокруг неё, и она уходит обратно на корабль.
/// «Гидры» и «Мантикоры» Империума с боезапасом, достающие до зоны, могут подбить капсулу на спуске.
/// Высаживаться можно и во время подготовки: зоны высадки лежат за бурей посреди поля, и до её конца к обороне не пройти.
/// </summary>
public sealed class CadiaDropPodSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;
    [Dependency] private readonly CadiaStatsSystem _stats = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;

    private const string PodPrototype = "CadiaDreadclaw";
    /// <summary>Сколько капсула стоит на земле, прежде чем уйти.</summary>
    private static readonly TimeSpan Grounded = TimeSpan.FromSeconds(6);

    /// <summary>Шанс попадания за каждое зенитное орудие, достающее до зоны, и предел.</summary>
    private const float HitPerGun = 0.2f;
    private const float HitMax = 0.6f;
    /// <summary>Удар капсулы: кто прямо под ней — урон и оглушение, кто рядом — сбит с ног.</summary>
    private const float CrushRadius = 1.3f;
    private const float ShockRadius = 3f;
    private const float CrushDamage = 35f;
    private const float WoundDamage = 25f;

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Heat = "Heat";

    private static readonly SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/Effects/shuttle_thruster.ogg", AudioParams.Default.WithVolume(2));
    private static readonly SoundSpecifier FallSound = new SoundPathSpecifier("/Audio/Imperial/Cadia/Ambience/far_blast2.ogg", AudioParams.Default.WithVolume(4));
    private static readonly SoundSpecifier ImpactSound = new SoundPathSpecifier("/Audio/Effects/explosion_small1.ogg", AudioParams.Default.WithVolume(6));
    private static readonly SoundSpecifier CrashSound = new SoundPathSpecifier("/Audio/Effects/explosion4.ogg", AudioParams.Default.WithVolume(6));
    private static readonly SoundSpecifier LiftSound = new SoundPathSpecifier("/Audio/Effects/shuttle_thruster.ogg", AudioParams.Default.WithVolume(4));

    /// <summary>Где выходят бойцы: вокруг капсулы.</summary>
    private static readonly Vector2[] Exits =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(-1, -1), new(1, -1), new(-1, 1),
    };

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaDropBerthComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<CadiaDropBerthComponent, DamageChangedEvent>(OnBerthDamaged);
        SubscribeLocalEvent<CadiaDropBerthComponent, CadiaBerthRepairDoAfterEvent>(OnRepaired);
    }

    // ───────────────────────── Разбитая шахта ─────────────────────────

    private void OnBerthDamaged(Entity<CadiaDropBerthComponent> ent, ref DamageChangedEvent args)
    {
#pragma warning disable CS0618
        var total = _damage.GetTotalDamage((ent.Owner, args.Damageable));
#pragma warning restore CS0618
        if (ent.Comp.Broken || total < ent.Comp.BreakAt)
            return;
        ent.Comp.Broken = true;
        ent.Comp.Fire = Spawn("CadiaWreckFire", Transform(ent).Coordinates);
        _audio.PlayPvs(CrashSound, ent);
        _rule.Announce(CadiaFaction.Chaos, Loc.GetString("cadia-berth-broken-chaos"), CrashSound);
        _rule.Feed(Loc.GetString("cadia-berth-broken-feed"), CadiaFaction.Imperium);
        _stats.Count(CadiaWarCount.BerthsWrecked);
    }

    private void BeginRepair(Entity<CadiaDropBerthComponent> ent, EntityUid user)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.RepairTime, new CadiaBerthRepairDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cadia-berth-repair-begin", ("user", user)), user, PopupType.Medium);
    }

    private void OnRepaired(Entity<CadiaDropBerthComponent> ent, ref CadiaBerthRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Broken)
            return;
        args.Handled = true;
        ent.Comp.Broken = false;
        _damage.SetAllDamage(ent.Owner, 0);
        if (ent.Comp.Fire is { } fire && !TerminatingOrDeleted(fire))
            QueueDel(fire);
        ent.Comp.Fire = null;
        _popup.PopupEntity(Loc.GetString("cadia-berth-repaired"), ent, PopupType.Medium);
    }

    private void OnVerbs(Entity<CadiaDropBerthComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        var user = args.User;
        if (ent.Comp.Broken)
        {
            if (TryComp<CadiaFactionMemberComponent>(user, out var fixer) && fixer.Role is { } role && ent.Comp.Repairers.Contains(role))
            {
                args.Verbs.Add(new AlternativeVerb
                {
                    Text = Loc.GetString("cadia-berth-repair-verb"),
                    Priority = 5,
                    Act = () => BeginRepair(ent, user),
                });
            }
            return;
        }
        var zones = EntityQueryEnumerator<CadiaDropZoneComponent>();
        while (zones.MoveNext(out var uid, out var zone))
        {
            var target = uid;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cadia-drop-verb", ("zone", Loc.GetString(zone.Label))),
                Category = VerbCategory.SelectType,
                Act = () => Launch(ent, user, target),
            });
        }
    }

    private void Launch(Entity<CadiaDropBerthComponent> berth, EntityUid user, EntityUid zone)
    {
        if (!TryComp<CadiaFactionMemberComponent>(user, out var member) || member.Faction != CadiaFaction.Chaos)
        {
            _popup.PopupEntity(Loc.GetString("cadia-drop-denied"), user, user, PopupType.MediumCaution);
            return;
        }
        if (!TryComp<CadiaDropZoneComponent>(zone, out var zoneComp))
            return;
        if (berth.Comp.Broken)
        {
            _popup.PopupEntity(Loc.GetString("cadia-berth-broken"), user, user, PopupType.MediumCaution);
            return;
        }
        var now = _timing.CurTime;
        if (now < berth.Comp.NextLaunch)
        {
            _popup.PopupEntity(Loc.GetString("cadia-drop-returning", ("seconds", (int) Math.Ceiling((berth.Comp.NextLaunch - now).TotalSeconds))),
                user, user, PopupType.MediumCaution);
            return;
        }

        // Отделение — живые слуги Хаоса у шахты; тот, кто отдал приказ, летит всегда.
        var passengers = new List<EntityUid> { user };
        foreach (var other in _lookup.GetEntitiesInRange<CadiaFactionMemberComponent>(Transform(berth).Coordinates, berth.Comp.Radius))
        {
            if (passengers.Count >= berth.Comp.Capacity)
                break;
            if (other.Owner != user && other.Comp.Faction == CadiaFaction.Chaos && _mobState.IsAlive(other) && !_container.IsEntityInContainer(other))
                passengers.Add(other);
        }
        berth.Comp.NextLaunch = now + berth.Comp.Cooldown;
        _audio.PlayPvs(LaunchSound, berth);
        foreach (var passenger in passengers)
            _popup.PopupEntity(Loc.GetString("cadia-drop-launch", ("zone", Loc.GetString(zoneComp.Label))), passenger, passenger, PopupType.Medium);
        SendPod(_xform.GetMapCoordinates(zone), passengers);
    }

    /// <summary>Капсула падает в точку. Зенитки, достающие до неё, могут попасть: тогда посадка жёсткая и мимо.</summary>
    public EntityUid SendPod(MapCoordinates target, List<EntityUid> passengers)
    {
        var now = _timing.CurTime;
        var hit = _random.Prob(FlakChance(target));
        if (hit)
            target = target.Offset(_random.NextVector2(2f, 4f));

        var pod = Spawn(PodPrototype, target);
        var visual = EnsureComp<CadiaDropPodComponent>(pod);
        visual.Start = now;
        visual.Leaving = false;
        Dirty(pod, visual);
        var flight = EnsureComp<CadiaDropPodFlightComponent>(pod);
        flight.Passengers = passengers;
        flight.Hit = hit;
        flight.Impact = now + TimeSpan.FromSeconds(visual.Duration);
        flight.LiftOff = flight.Impact + Grounded;
        flight.Gone = flight.LiftOff + TimeSpan.FromSeconds(visual.Duration);
        if (FlakChance(target) > 0f)
        {
            for (var i = 0; i < (hit ? 5 : 3); i++)
                flight.Flak.Add(now + TimeSpan.FromSeconds(_random.NextFloat(0.2f, visual.Duration - 0.3f)));
        }
        _audio.PlayPvs(FallSound, pod);
        return pod;
    }

    /// <summary>Шанс попадания ПВО Империума по капсуле над точкой.</summary>
    private float FlakChance(MapCoordinates target)
    {
        var chance = 0f;
        var guns = EntityQueryEnumerator<CadiaAntiAirComponent, TransformComponent>();
        while (guns.MoveNext(out var uid, out var gun, out var xform))
        {
            if (gun.Faction != CadiaFaction.Imperium || gun.Ammo <= 0 || xform.MapID != target.MapId)
                continue;
            if ((_xform.GetWorldPosition(xform) - target.Position).Length() <= gun.Range * 1.5f)
                chance += HitPerGun;
        }
        return Math.Min(chance, HitMax);
    }

    /// <summary>Перезарядить все пусковые шахты (панель ведущего).</summary>
    public void RechargeBerths()
    {
        var berths = EntityQueryEnumerator<CadiaDropBerthComponent>();
        while (berths.MoveNext(out var berth))
            berth.NextLaunch = TimeSpan.Zero;
    }

    /// <summary>Пустая капсула в случайную зону (панель ведущего): проверить зону или напугать оборону.</summary>
    public bool SendEmptyToRandomZone()
    {
        var zones = new List<EntityUid>();
        var query = EntityQueryEnumerator<CadiaDropZoneComponent>();
        while (query.MoveNext(out var uid, out _))
            zones.Add(uid);
        if (zones.Count == 0)
            return false;
        SendPod(_xform.GetMapCoordinates(_random.Pick(zones)), new List<EntityUid>());
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var pods = EntityQueryEnumerator<CadiaDropPodFlightComponent, CadiaDropPodComponent>();
        var gone = new List<EntityUid>();
        while (pods.MoveNext(out var uid, out var flight, out var visual))
        {
            for (var i = flight.Flak.Count - 1; i >= 0; i--)
            {
                if (now < flight.Flak[i])
                    continue;
                flight.Flak.RemoveAt(i);
                Spawn("CadiaFlakBurst", _xform.GetMapCoordinates(uid).Offset(_random.NextVector2(0.5f, 2.5f)));
            }

            if (!flight.Landed && now >= flight.Impact)
            {
                flight.Landed = true;
                Land(uid, flight);
            }
            else if (flight.Landed && !visual.Leaving && now >= flight.LiftOff)
            {
                visual.Leaving = true;
                visual.Start = now;
                Dirty(uid, visual);
                _audio.PlayPvs(LiftSound, uid);
            }
            else if (now >= flight.Gone)
            {
                gone.Add(uid);
            }
        }
        foreach (var uid in gone)
            QueueDel(uid);
    }

    /// <summary>
    /// Удар о землю: кто под капсулой — ранен и оглушён, кто рядом — сбит с ног. Отделение выходит вокруг капсулы;
    /// если её подбили, бойцы выходят из обломков раненые.
    /// </summary>
    private void Land(EntityUid pod, CadiaDropPodFlightComponent flight)
    {
        var coords = Transform(pod).Coordinates;
        var map = _xform.GetMapCoordinates(pod);
        _audio.PlayPvs(flight.Hit ? CrashSound : ImpactSound, pod);
        Spawn("CadiaJumpImpactEffect", map);

        _mobs.Clear();
        _lookup.GetEntitiesInRange(coords, ShockRadius, _mobs);
        var blunt = _proto.Index(Blunt);
        foreach (var mob in _mobs)
        {
            if (flight.Passengers.Contains(mob) || !_mobState.IsAlive(mob))
                continue;
            if ((_xform.GetMapCoordinates(mob).Position - map.Position).Length() <= CrushRadius)
            {
                _damage.TryChangeDamage(mob.Owner, new DamageSpecifier(blunt, CrushDamage), origin: pod);
                _stun.TryUpdateParalyzeDuration(mob, TimeSpan.FromSeconds(3));
            }
            else
            {
                _stun.TryUpdateParalyzeDuration(mob, TimeSpan.FromSeconds(1.5));
            }
        }

        var wound = new DamageSpecifier(blunt, WoundDamage);
        wound.DamageDict[Heat] = 10;
        var i = 0;
        foreach (var passenger in flight.Passengers)
        {
            if (TerminatingOrDeleted(passenger) || !_mobState.IsAlive(passenger) || _container.IsEntityInContainer(passenger))
                continue;
            _xform.SetCoordinates(passenger, coords.Offset(Exits[i++ % Exits.Length]));
            _xform.AttachToGridOrMap(passenger);
            if (!flight.Hit)
                continue;
            _damage.TryChangeDamage(passenger, wound, origin: pod);
            _stun.TryUpdateParalyzeDuration(passenger, TimeSpan.FromSeconds(2.5));
            _popup.PopupEntity(Loc.GetString("cadia-drop-hit"), passenger, passenger, PopupType.LargeCaution);
        }
        if (flight.Hit)
        {
            _rule.Feed(Loc.GetString("cadia-drop-hit-feed"), CadiaFaction.Imperium);
            _stats.Count(CadiaWarCount.PodsShotDown);
        }
        if (flight.Passengers.Count > 0)
            _stats.Count(CadiaWarCount.PodsLanded);
        flight.Passengers.Clear();
    }
}
