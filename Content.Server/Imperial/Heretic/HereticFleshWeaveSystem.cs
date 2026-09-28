using Content.Server.Popups;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticFleshWeaveSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem   _doAfter   = default!;
    [Dependency] private readonly UserInterfaceSystem   _ui        = default!;
    [Dependency] private readonly PopupSystem           _popup     = default!;
    [Dependency] private readonly MobStateSystem        _mobState  = default!;
    [Dependency] private readonly SharedHandsSystem     _hands     = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, HereticFleshWeaveActionEvent>(OnFleshWeave);
        SubscribeLocalEvent<HereticKnowledgeHolderComponent, HereticFleshWeaveSelectOrganMessage>(OnSelectOrgan);
        SubscribeLocalEvent<HereticComponent, HereticFleshWeaveDoAfterEvent>(OnDoAfter);
    }

    private void OnFleshWeave(EntityUid uid, HereticComponent comp, HereticFleshWeaveActionEvent args)
    {
        if (args.Handled) return;

        var target = args.Target;

        if (!TryComp<BodyComponent>(target, out _))
        {
            _popup.PopupEntity(Loc.GetString("heretic-flesh-weave-no-body"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!_container.TryGetContainer(target, BodyComponent.ContainerID, out var organsContainer))
            return;

        var organList = new List<HereticFleshWeaveOrganData>();
        foreach (var organ in organsContainer.ContainedEntities)
        {
            if (!HasComp<OrganComponent>(organ)) continue;
            organList.Add(new HereticFleshWeaveOrganData
            {
                Organ = GetNetEntity(organ),
                Name  = MetaData(organ).EntityName,
            });
        }

        if (organList.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-flesh-weave-no-organs"), uid, uid, PopupType.SmallCaution);
            return;
        }

        _ui.SetUiState(comp.BuiHolder, HereticFleshWeaveOrganBuiKey.Key, new HereticFleshWeaveOrganBuiState
        {
            Organs = organList,
            Target = GetNetEntity(target),
        });
        _ui.TryOpenUi(comp.BuiHolder, HereticFleshWeaveOrganBuiKey.Key, uid);

        args.Handled = true;
    }

    private void OnSelectOrgan(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, HereticFleshWeaveSelectOrganMessage args)
    {
        var hereticUid = args.Actor;
        if (!TryComp<HereticComponent>(hereticUid, out _)) return;

        var organ  = GetEntity(args.Organ);
        var target = GetEntity(args.Target);

        if (!HasComp<OrganComponent>(organ)) return;
        if (!Exists(target)) return;

        var isDead = _mobState.IsDead(target);
        var delay  = isDead ? 2f : 8f;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            hereticUid,
            TimeSpan.FromSeconds(delay),
            new HereticFleshWeaveDoAfterEvent { Organ = args.Organ, Target = args.Target },
            hereticUid)
        {
            BreakOnMove   = true,
            BreakOnDamage = true,
            NeedHand      = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfter(EntityUid uid, HereticComponent comp, HereticFleshWeaveDoAfterEvent args)
    {
        if (args.Cancelled) return;

        var organ  = GetEntity(args.Organ);
        var target = GetEntity(args.Target);

        if (!HasComp<OrganComponent>(organ)) return;
        if (!Exists(target)) return;

        if (!_container.TryGetContainer(target, BodyComponent.ContainerID, out var organsContainer)) return;

        _container.Remove(organ, organsContainer);
        _hands.TryPickupAnyHand(uid, organ);
    }
}
