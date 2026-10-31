using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Место ритуала над завесой: круг в часовне флагмана для Хаоса, пилон для Империума.</summary>
[RegisterComponent]
public sealed partial class CadiaVeilRitualComponent : Component
{
    [DataField(required: true)]
    public CadiaFaction Faction;

    /// <summary>Кто может вести ритуал.</summary>
    [DataField(required: true)]
    public List<string> Roles = new();

    /// <summary>На сколько ритуал сдвигает конец подготовки: Хаос — раньше, Империум — позже.</summary>
    [DataField]
    public TimeSpan Shift = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);
}

/// <summary>След ритуала: бойцы стороны рядом понемногу теряют или набирают мораль.</summary>
[RegisterComponent]
public sealed partial class CadiaRitualMarkComponent : Component
{
    [DataField(required: true)]
    public CadiaFaction Faction;

    /// <summary>Сколько морали за один такт (раз в две секунды).</summary>
    [DataField(required: true)]
    public float Delta;

    [DataField]
    public float Radius = 4f;
}

/// <summary>
/// Борьба за завесу, пока идёт подготовка. Колдуны и апостолы Хаоса у ритуального круга флагмана раскачивают её —
/// обстрел начинается раньше; жрецы, псайкеры, астропаты и техножрецы у пилонов поют литанию — завеса держится дольше,
/// но не дольше, чем отведено на подготовку. У каждой стороны свой перерыв между ритуалами.
/// Ритуал оставляет след на поле: колдовство — трещину завесы у случайного пилона, где гвардию гложет страх;
/// литания — освящённую землю у пилона, где защитники держатся твёрже. Следы остаются до конца боя.
/// </summary>
public sealed class CadiaVeilRitualSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;
    [Dependency] private readonly CadiaStatsSystem _stats = default!;
    [Dependency] private readonly CadiaMoraleSystem _morale = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly Robust.Shared.Random.IRobustRandom _random = default!;

    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(90);
    /// <summary>Ритуал Хаоса не сократит подготовку меньше этого.</summary>
    private static readonly TimeSpan MinLeft = TimeSpan.FromSeconds(60);
    private static readonly SoundSpecifier ChaosSound = new SoundPathSpecifier("/Audio/Imperial/Cadia/Ambience/rift_drone.ogg");
    private static readonly SoundSpecifier ImperiumSound = new SoundPathSpecifier("/Audio/Imperial/Cadia/Ambience/bell_toll.ogg");

    private readonly Dictionary<CadiaFaction, TimeSpan> _next = new();
    private static readonly TimeSpan AuraTick = TimeSpan.FromSeconds(2);
    private TimeSpan _nextAura;
    private readonly HashSet<Entity<CadiaFactionMemberComponent>> _near = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextAura)
            return;
        _nextAura = now + AuraTick;
        var auras = EntityQueryEnumerator<CadiaRitualMarkComponent, TransformComponent>();
        while (auras.MoveNext(out _, out var aura, out var xform))
        {
            _near.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, aura.Radius, _near);
            foreach (var member in _near)
            {
                if (member.Comp.Faction == aura.Faction && member.Comp.Role != null)
                    _morale.Change(member, aura.Delta);
            }
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaVeilRitualComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<CadiaVeilRitualComponent, CadiaVeilRitualDoAfterEvent>(OnDone);
    }

    private void OnVerbs(Entity<CadiaVeilRitualComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<CadiaFactionMemberComponent>(args.User, out var member) ||
            member.Faction != ent.Comp.Faction || member.Role is not { } role || !ent.Comp.Roles.Contains(role))
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Faction == CadiaFaction.Chaos ? "cadia-ritual-verb-chaos" : "cadia-ritual-verb-imperium"),
            Priority = 5,
            Act = () => Begin(ent, user),
        });
    }

    private void Begin(Entity<CadiaVeilRitualComponent> ent, EntityUid user)
    {
        if (!_rule.TryGetActiveWar(out var war) || war.Phase != CadiaWarPhase.Preparation || war.VeilOverride != null)
        {
            _popup.PopupEntity(Loc.GetString("cadia-ritual-not-now"), user, user, PopupType.MediumCaution);
            return;
        }
        var now = _timing.CurTime;
        if (_next.TryGetValue(ent.Comp.Faction, out var next) && now < next)
        {
            _popup.PopupEntity(Loc.GetString("cadia-ritual-cooldown", ("seconds", (int) Math.Ceiling((next - now).TotalSeconds))),
                user, user, PopupType.MediumCaution);
            return;
        }
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.Duration, new CadiaVeilRitualDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
            return;
        _popup.PopupEntity(Loc.GetString(ent.Comp.Faction == CadiaFaction.Chaos ? "cadia-ritual-begin-chaos" : "cadia-ritual-begin-imperium",
            ("user", user)), user, PopupType.Medium);
        _audio.PlayPvs(ent.Comp.Faction == CadiaFaction.Chaos ? ChaosSound : ImperiumSound, ent);
    }

    private void OnDone(Entity<CadiaVeilRitualComponent> ent, ref CadiaVeilRitualDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;
        if (!_rule.TryGetActiveRule(out var rule, out var war) || war.Phase != CadiaWarPhase.Preparation || war.VeilOverride != null)
            return;

        var now = _timing.CurTime;
        _next[ent.Comp.Faction] = now + Cooldown;
        var before = war.PhaseEnd;
        if (ent.Comp.Faction == CadiaFaction.Chaos)
        {
            var earliest = now + MinLeft;
            war.PhaseEnd = before - ent.Comp.Shift < earliest ? (before < earliest ? before : earliest) : before - ent.Comp.Shift;
        }
        else
        {
            var latest = war.PhaseStart + rule.Comp.PreparationTime;
            war.PhaseEnd = before + ent.Comp.Shift > latest ? (before > latest ? before : latest) : before + ent.Comp.Shift;
        }
        _rule.DirtyWar();

        var seconds = (int) Math.Abs((war.PhaseEnd - before).TotalSeconds);
        var key = ent.Comp.Faction == CadiaFaction.Chaos ? "chaos" : "imperium";
        _rule.Announce(CadiaFaction.Imperium, Loc.GetString($"cadia-ritual-done-{key}-imperium", ("seconds", seconds)),
            ent.Comp.Faction == CadiaFaction.Chaos ? ChaosSound : ImperiumSound);
        _rule.Announce(CadiaFaction.Chaos, Loc.GetString($"cadia-ritual-done-{key}-chaos", ("seconds", seconds)),
            ent.Comp.Faction == CadiaFaction.Chaos ? ChaosSound : ImperiumSound);
        Spawn(ent.Comp.Faction == CadiaFaction.Chaos ? "CadiaPsyRingRed" : "CadiaFaithRingEffect", _xform.GetMapCoordinates(ent));
        _stats.Count(ent.Comp.Faction == CadiaFaction.Chaos ? CadiaWarCount.RitualsChaos : CadiaWarCount.RitualsImperium);
        LeaveMark(ent);
    }

    /// <summary>След ритуала на поле: трещина у случайного пилона или освящённая земля у того, где пели литанию.</summary>
    private void LeaveMark(Entity<CadiaVeilRitualComponent> ent)
    {
        if (ent.Comp.Faction == CadiaFaction.Imperium)
        {
            Spawn("CadiaHallowedGround", _xform.GetMapCoordinates(ent).Offset(_random.NextVector2(1.5f, 2.5f)));
            return;
        }
        var pylons = new List<EntityUid>();
        var query = EntityQueryEnumerator<CadiaPylonComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != null)
                pylons.Add(uid);
        }
        if (pylons.Count > 0)
            Spawn("CadiaVeilCrack", _xform.GetMapCoordinates(_random.Pick(pylons)).Offset(_random.NextVector2(2f, 4f)));
    }
}
