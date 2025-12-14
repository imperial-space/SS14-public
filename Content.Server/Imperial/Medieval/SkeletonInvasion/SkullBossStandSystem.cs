using System.Linq;
using Content.Server.Bible.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server.Hands.Systems;
using Content.Server.Jittering;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.SkeletonInvasion;
using Content.Shared.Interaction;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.SkeletonInvasion;

public sealed class SkullBossStandSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly JitteringSystem _jitter = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkullBossStandPartComponent, AfterInteractEvent>(OnAfterPartInteract);
        SubscribeLocalEvent<SkullBossStandPartComponent, AfterInteractUsingEvent>(OnAfterPartInteractUsing);
        SubscribeLocalEvent<SkullBossStandPartComponent, SkullPartAttachmentDoAfterEvent>(OnAttachDoAfter);
        SubscribeLocalEvent<SkullBossStandPartComponent, SkullPartPurifyingDoAfterEvent>(OnPurifyDoAfter);
    }

    private void OnAfterPartInteract(EntityUid uid, SkullBossStandPartComponent comp, AfterInteractEvent args)
    {
        if (!TryComp<SkullBossStandComponent>(args.Target, out var stand))
            return;

        if (stand.AttachedParts.ContainsKey(comp.Idx))
        {
            _popup.PopupEntity("Эта часть уже присутствует.", args.Target.Value, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, 7, new SkullPartAttachmentDoAfterEvent(), uid, args.Target.Value, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnDropItem = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnAfterPartInteractUsing(EntityUid uid, SkullBossStandPartComponent comp, AfterInteractUsingEvent args)
    {
        if (!HasComp<BibleComponent>(args.Used))
            return;

        if (comp.Purified)
        {
            _popup.PopupEntity("Эта часть уже освящена.", uid, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, 40, new SkullPartPurifyingDoAfterEvent(), uid, uid, args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnDropItem = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnAttachDoAfter(EntityUid uid, SkullBossStandPartComponent comp, SkullPartAttachmentDoAfterEvent args)
    {
        if (!TryComp<SkullBossStandComponent>(args.Target, out var stand) || stand.AttachedParts.Count >= stand.RequiredParts || args.Cancelled)
            return;

        if (stand.AttachedParts.ContainsKey(comp.Idx))
        {
            _popup.PopupEntity("Эта часть уже присутствует.", args.Target.Value, args.User);
            return;
        }

        stand.AttachedParts.Add(comp.Idx, comp.Purified);
        stand.AttachedProtos.Add(Prototype(uid)!.ID);
        PartAttached(args.Target.Value, stand);
        Dirty(args.Target.Value, stand);

        _transform.DetachEntity(uid, Transform(uid));
    }

    private void OnPurifyDoAfter(EntityUid uid, SkullBossStandPartComponent comp, SkullPartPurifyingDoAfterEvent args)
    {
        if (!HasComp<BibleComponent>(args.Used) || args.Cancelled)
            return;

        if (comp.Purified)
        {
            _popup.PopupEntity("Эта часть уже освящена.", uid, args.User);
            return;
        }

        comp.Purified = true;
        _appearance.SetData(uid, SkullStandPartAppearance.Key, true);
        _popup.PopupEntity("Вы освятили часть черепа.", uid, args.User);
    }

    private void PartAttached(EntityUid uid, SkullBossStandComponent comp)
    {
        if (comp.AttachedParts.Count >= comp.RequiredParts)
        {
            _audio.PlayPvs(comp.CompleteSound, uid);
            _jitter.DoJitter(uid, TimeSpan.FromSeconds(5), true, frequency: 6);
            Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(5), () =>
            {
                RaiseLocalEvent(new SkullBossStandCompletedEvent(uid, comp.AttachedParts.Count, comp.AttachedParts.Where(x => x.Value == true).Count()));
            });
        }
        _audio.PlayPvs(comp.AttachSound, uid);
        _jitter.DoJitter(uid, TimeSpan.FromSeconds(2), true, frequency: 6);
        if (!comp.Announcements.TryGetValue(comp.AttachedParts.Count, out var announce))
            return;

        _chat.ChatMessageToAll(ChatChannel.Radio, announce, announce, EntityUid.Invalid, false, true, colorOverride: Color.DeepPink);
        _audio.PlayGlobal(new SoundPathSpecifier(new ResPath("/Audio/Imperial/Medieval/Effects/skull-announce.ogg")), Filter.Broadcast(), true);
    }
}

