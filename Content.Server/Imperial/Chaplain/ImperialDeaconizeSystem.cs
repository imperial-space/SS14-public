using Content.Server.Bible;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Cult;
using Content.Shared.Chat;
using Content.Shared.Climbing.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.Chaplain;
using Content.Shared.Imperial.Chaplain.Components;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Prayer;
using Content.Server.Administration.Logs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.GameTicking;

namespace Content.Server.Imperial.Chaplain;

/// <summary>
/// Обряд «Посвящение в дьякона» (religion_rites/deaconize из SS13): жрец бьёт библией по алтарю,
/// на котором лежит кандидат. Сначала кандидату приходит приглашение, после согласия повторный удар
/// начинает обряд из пяти воззваний. Обряд одноразовый: посвятить можно только одного человека за раунд.
/// </summary>
public sealed class ImperialDeaconizeSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly CultSystem _cult = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly ImperialReligionSystem _religion = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    /// <summary>ritual_length / количество воззваний: 30 секунд на пять воззваний.</summary>
    private static readonly TimeSpan InvocationDelay = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan InviteTimeout = TimeSpan.FromSeconds(60);
    private static readonly SoundSpecifier RiteSound = new SoundPathSpecifier("/Audio/Effects/holy.ogg");
    private const int InvocationCount = 5;

    /// <summary>Радиус раскрытия рун от святого (range(2, user) в SS13).</summary>
    private const float RevealRange = 2.5f;
    private static readonly SoundSpecifier FloorSmackSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    /// <summary>Обряд одноразовый (RITE_ONE_TIME_USE).</summary>
    private bool _used;

    /// <summary>Кандидат, принявший приглашение (potential_deacon).</summary>
    private EntityUid? _potentialDeacon;

    private EntityUid? _invited;
    private TimeSpan _inviteExpires;

    private EntityUid? _riteAltar;
    private EntityUid? _riteCandidate;
    private int _riteStep;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialHolyBibleComponent, AfterInteractEvent>(OnBibleAfterInteract,
            before: new[] { typeof(BibleSystem) });
        SubscribeLocalEvent<ImperialHolyComponent, ImperialDeaconizeDoAfterEvent>(OnInvocation);
        SubscribeNetworkEvent<ImperialDeaconInviteResponseEvent>(OnInviteResponse);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _used = false;
        _potentialDeacon = null;
        _invited = null;
        ResetRite();
    }

    private void ResetRite()
    {
        _riteAltar = null;
        _riteCandidate = null;
        _riteStep = 0;
    }

    /// <summary>
    /// Удар библией по полу (bible/interact_with_atom в SS13): святой раскрывает скрытые руны
    /// и строения культа в радиусе 2 клеток от себя.
    /// </summary>
    private void SmackFloor(ref AfterInteractEvent args)
    {
        var user = args.User;
        if (!HasComp<ImperialHolyComponent>(user))
            return;

        args.Handled = true;
        var revealed = _cult.RevealConcealed(_xform.GetMapCoordinates(user), RevealRange);
        _popup.PopupCoordinates(Loc.GetString(revealed > 0 ? "imperial-bible-floor-revealed" : "imperial-bible-floor-smacked"),
            args.ClickLocation,
            user);
        _audio.PlayPvs(FloorSmackSound, args.ClickLocation);
    }

    /// <summary>Алтари SS14: на них можно молиться и забираться.</summary>
    private bool IsAltar(EntityUid uid)
    {
        return HasComp<PrayableComponent>(uid) && HasComp<ClimbableComponent>(uid);
    }

    private void OnBibleAfterInteract(Entity<ImperialHolyBibleComponent> bible, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (args.Target == null)
        {
            SmackFloor(ref args);
            return;
        }

        if (args.Target is not { } altar || !IsAltar(altar))
            return;

        var user = args.User;
        if (!TryComp<ImperialHolyComponent>(user, out var holy))
            return;

        args.Handled = true;

        if (holy.Role < HolyRole.Priest)
        {
            _popup.PopupEntity(Loc.GetString("imperial-deaconize-only-priests"), user, user);
            return;
        }

        if (_riteAltar != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-deaconize-in-progress"), user, user);
            return;
        }

        if (_used)
        {
            _popup.PopupEntity(Loc.GetString("imperial-deaconize-used"), user, user);
            return;
        }

        if (FindCandidate(altar, user) is not { } candidate)
        {
            _popup.PopupEntity(Loc.GetString("imperial-deaconize-nobody", ("altar", altar)), user, user);
            return;
        }

        if (!IsValidCandidate(candidate, user))
            return;

        // Никто не приглашён или на алтаре не тот, кто согласился: сначала приглашение.
        if (_potentialDeacon != candidate)
        {
            Invite(candidate);
            _popup.PopupEntity(Loc.GetString("imperial-deaconize-invited"), user, user);
            return;
        }

        _riteAltar = altar;
        _riteCandidate = candidate;
        _riteStep = 0;
        if (!StartInvocation(user, altar, bible))
            ResetRite();
    }

    private EntityUid? FindCandidate(EntityUid altar, EntityUid user)
    {
        foreach (var uid in _lookup.GetEntitiesIntersecting(altar, LookupFlags.Dynamic))
        {
            if (uid != user && HasComp<MindContainerComponent>(uid) && HasComp<MobStateComponent>(uid))
                return uid;
        }

        return null;
    }

    private bool IsValidCandidate(EntityUid candidate, EntityUid user)
    {
        if (!_mobState.IsAlive(candidate))
        {
            _popup.PopupEntity(Loc.GetString("imperial-deaconize-not-conscious", ("target", candidate)), user, user);
            return false;
        }

        if (HasComp<ImperialHolyComponent>(candidate))
        {
            _popup.PopupEntity(Loc.GetString("imperial-deaconize-already-holy", ("target", candidate)), user, user);
            return false;
        }

        return true;
    }

    private void Invite(EntityUid candidate)
    {
        if (!TryComp<ActorComponent>(candidate, out var actor))
            return;

        if (_invited == candidate && _timing.CurTime < _inviteExpires)
            return;

        _invited = candidate;
        _inviteExpires = _timing.CurTime + InviteTimeout;
        RaiseNetworkEvent(new ImperialDeaconInviteEvent(_religion.Deity), actor.PlayerSession);
    }

    private void OnInviteResponse(ImperialDeaconInviteResponseEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid || uid != _invited)
            return;

        _invited = null;
        if (!ev.Accepted || _timing.CurTime > _inviteExpires)
            return;

        _potentialDeacon = uid;
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(uid):player} accepted the invitation to become a deacon");
    }

    private bool StartInvocation(EntityUid user, EntityUid altar, EntityUid bible)
    {
        var args = new DoAfterArgs(EntityManager, user, InvocationDelay, new ImperialDeaconizeDoAfterEvent(), user, altar, bible)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        return _doAfter.TryStartDoAfter(args);
    }

    private void OnInvocation(Entity<ImperialHolyComponent> ent, ref ImperialDeaconizeDoAfterEvent args)
    {
        if (args.Handled || _riteAltar is not { } altar || _riteCandidate is not { } candidate)
            return;

        args.Handled = true;
        if (args.Cancelled)
        {
            ResetRite();
            return;
        }

        _riteStep++;
        _chat.TrySendInGameICMessage(ent, Loc.GetString($"imperial-deaconize-invocation-{_riteStep}"), InGameICChatType.Speak, false);

        if (_riteStep < InvocationCount)
        {
            args.Repeat = true;
            return;
        }

        _chat.TrySendInGameICMessage(ent, Loc.GetString("imperial-deaconize-invoke"), InGameICChatType.Speak, false);
        ResetRite();
        InvokeEffect(ent, altar, candidate);
    }

    private void InvokeEffect(EntityUid user, EntityUid altar, EntityUid candidate)
    {
        // Последняя проверка, что на алтаре всё ещё тот же кандидат.
        if (Deleted(candidate) || FindCandidate(altar, user) != candidate)
        {
            _popup.PopupEntity(Loc.GetString("imperial-deaconize-left", ("target", candidate)), user, user);
            return;
        }

        if (!_mobState.IsAlive(candidate))
        {
            _popup.PopupEntity(Loc.GetString("imperial-deaconize-not-conscious", ("target", candidate)), user, user);
            return;
        }

        if (!_mind.TryGetMind(candidate, out _, out _))
        {
            _popup.PopupEntity(Loc.GetString("imperial-deaconize-no-mind", ("target", candidate)), user, user);
            return;
        }

        _audio.PlayPvs(RiteSound, altar);
        _potentialDeacon = null;

        // Бог видит зло в сердце культиста.
        if (HasComp<CultistComponent>(candidate))
        {
            _popup.PopupEntity(Loc.GetString("imperial-deaconize-cultist", ("deity", _religion.Deity), ("target", candidate)),
                altar,
                PopupType.LargeCaution);
            _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(user):player} gibbed cultist {ToPrettyString(candidate):player} with the deaconize rite");
            _gibbing.Gib(candidate, user: user);
            return;
        }

        _used = true;
        _religion.MakeHoly(candidate, HolyRole.Deacon);
        _popup.PopupEntity(Loc.GetString("imperial-deaconize-success", ("deity", _religion.Deity), ("target", candidate)), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):player} deaconized {ToPrettyString(candidate):player}");
    }
}
