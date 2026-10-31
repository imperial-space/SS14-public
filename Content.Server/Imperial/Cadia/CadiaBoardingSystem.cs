using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Абордажная рампа «Валькирии»: отсюда штурмовая группа прорывается на флагман Хаоса.</summary>
[RegisterComponent]
public sealed partial class CadiaBoardingRampComponent : Component
{
    [DataField]
    public float Radius = 1.5f;

    [DataField]
    public int Capacity = 8;

    /// <summary>Как близко к флагману должна подойти «Валькирия».</summary>
    [DataField]
    public float Reach = 45f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan NextUse;
}

/// <summary>Место прорыва абордажной группы на флагмане.</summary>
[RegisterComponent]
public sealed partial class CadiaBoardingPointComponent : Component
{
    [DataField(required: true)]
    public LocId Label;
}

/// <summary>
/// Абордаж флагмана. «Валькирия» поднимается на орбиту и подходит к «Терзателю»; гвардейцы у абордажной рампы прорезают
/// корпус и высаживаются на выбранной палубе — сорвать батареи, разбить пусковые шахты, убить колдуна у круга. Хаос слышит
/// тревогу. Назад — с той же точки прорыва, пока «Валькирия» рядом. Пока держится завеса, на орбиту не подняться.
/// </summary>
public sealed class CadiaBoardingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;
    [Dependency] private readonly CadiaStatsSystem _stats = default!;

    private static readonly SoundSpecifier BreachSound = new SoundPathSpecifier("/Audio/Effects/explosion_small2.ogg", AudioParams.Default.WithVolume(4));
    private static readonly SoundSpecifier AlarmSound = new SoundPathSpecifier("/Audio/Imperial/Mission_Blackout/A_Alarm2.ogg");

    private static readonly Vector2[] Spread =
    {
        Vector2.Zero, new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(-1, -1), new(1, -1),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaBoardingRampComponent, GetVerbsEvent<AlternativeVerb>>(OnRampVerbs);
        SubscribeLocalEvent<CadiaBoardingPointComponent, GetVerbsEvent<AlternativeVerb>>(OnPointVerbs);
    }

    private void OnRampVerbs(Entity<CadiaBoardingRampComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !IsImperial(args.User))
            return;
        var user = args.User;
        var points = EntityQueryEnumerator<CadiaBoardingPointComponent>();
        while (points.MoveNext(out var uid, out var point))
        {
            var target = uid;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cadia-boarding-verb", ("deck", Loc.GetString(point.Label))),
                Category = VerbCategory.SelectType,
                Act = () => Board(ent, user, target),
            });
        }
    }

    private void OnPointVerbs(Entity<CadiaBoardingPointComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !IsImperial(args.User))
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cadia-boarding-return-verb"),
            Act = () => Return(ent, user),
        });
    }

    private bool IsImperial(EntityUid uid)
    {
        return TryComp<CadiaFactionMemberComponent>(uid, out var member) && member.Faction == CadiaFaction.Imperium && member.Role != null;
    }

    private void Board(Entity<CadiaBoardingRampComponent> ramp, EntityUid user, EntityUid point)
    {
        var now = _timing.CurTime;
        if (_rule.TryGetActiveWar(out var war) && CadiaPylonVeilSystem.VeilUp(war))
        {
            _popup.PopupEntity(Loc.GetString("cadia-veil-sky"), user, user, PopupType.MediumCaution);
            return;
        }
        if (now < ramp.Comp.NextUse)
        {
            _popup.PopupEntity(Loc.GetString("cadia-boarding-cooldown", ("seconds", (int) Math.Ceiling((ramp.Comp.NextUse - now).TotalSeconds))),
                user, user, PopupType.MediumCaution);
            return;
        }
        if (!InReach(ramp, point))
        {
            _popup.PopupEntity(Loc.GetString("cadia-boarding-too-far"), user, user, PopupType.MediumCaution);
            return;
        }
        ramp.Comp.NextUse = now + ramp.Comp.Cooldown;

        // Группа — живые гвардейцы у рампы; тот, кто отдал приказ, идёт первым.
        var party = new List<EntityUid> { user };
        foreach (var other in _lookup.GetEntitiesInRange<CadiaFactionMemberComponent>(Transform(ramp).Coordinates, ramp.Comp.Radius))
        {
            if (party.Count >= ramp.Comp.Capacity)
                break;
            if (other.Owner != user && other.Comp.Faction == CadiaFaction.Imperium && _mobState.IsAlive(other) && !_container.IsEntityInContainer(other))
                party.Add(other);
        }

        var coords = Transform(point).Coordinates;
        _audio.PlayPvs(BreachSound, ramp);
        _audio.PlayPvs(BreachSound, point);
        Spawn("CadiaJumpImpactEffect", _xform.GetMapCoordinates(point));
        for (var i = 0; i < party.Count; i++)
        {
            _xform.SetCoordinates(party[i], coords.Offset(Spread[i % Spread.Length] + _random.NextVector2(0.2f)));
            _xform.AttachToGridOrMap(party[i]);
        }

        var deck = Loc.GetString(Comp<CadiaBoardingPointComponent>(point).Label);
        _rule.Announce(CadiaFaction.Chaos, Loc.GetString("cadia-boarding-alarm", ("deck", deck), ("count", party.Count)), AlarmSound);
        _rule.Feed(Loc.GetString("cadia-boarding-feed", ("deck", deck)), CadiaFaction.Imperium);
        _stats.Count(CadiaWarCount.Boardings);
    }

    private void Return(Entity<CadiaBoardingPointComponent> point, EntityUid user)
    {
        var ramps = EntityQueryEnumerator<CadiaBoardingRampComponent>();
        while (ramps.MoveNext(out var uid, out var ramp))
        {
            if (!InReach((uid, ramp), point))
                continue;
            _xform.SetCoordinates(user, Transform(uid).Coordinates);
            _xform.AttachToGridOrMap(user);
            _audio.PlayPvs(BreachSound, uid);
            return;
        }
        _popup.PopupEntity(Loc.GetString("cadia-boarding-no-ship"), user, user, PopupType.MediumCaution);
    }

    /// <summary>«Валькирия» на той же орбите и рядом с флагманом.</summary>
    private bool InReach(Entity<CadiaBoardingRampComponent> ramp, EntityUid point)
    {
        var from = _xform.GetMapCoordinates(ramp);
        var to = _xform.GetMapCoordinates(point);
        return from.MapId == to.MapId && (from.Position - to.Position).Length() <= ramp.Comp.Reach;
    }
}
