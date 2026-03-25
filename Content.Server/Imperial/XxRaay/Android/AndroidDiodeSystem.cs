using System;
using Content.Server.Hands.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.XxRaay.Android;
using Content.Shared.Popups;
using Content.Shared.Sprite;
using Content.Shared.Kitchen.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Localization;

namespace Content.Server.Imperial.XxRaay.Android;

/// <summary>
/// Серверная система, управляющая вырезанием диода у андроида
/// </summary>
public sealed class AndroidDiodeSystem : EntitySystem
{
    private static readonly TimeSpan CutDuration = TimeSpan.FromSeconds(30);

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AndroidDiodeComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<AndroidDiodeComponent, AndroidDiodeCutDoAfterEvent>(OnCutDoAfter);
    }

    private void OnGetVerbs(Entity<AndroidDiodeComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User != args.Target)
            return;

        var comp = ent.Comp;

        if (!comp.HasDiode || comp.IsCuttingInProgress)
            return;

        if (!TryComp<HandsComponent>(args.User, out var hands))
            return;

        var activeItem = _hands.GetActiveItem((args.User, hands));
        if (activeItem is null)
            return;

        var hasCuttingTool =
            TryComp<ToolComponent>(activeItem.Value, out var toolComp) &&
            _tool.HasQuality(activeItem.Value, SharedToolSystem.CutQuality, toolComp);

        var hasSharp = HasComp<SharpComponent>(activeItem.Value);

        if (!hasCuttingTool && !hasSharp)
            return;

        var user = args.User;

        var verb = new Verb
        {
            Text = Loc.GetString("android-diode-cut-verb"),
            Act = () => StartCut(ent.Owner, user, activeItem.Value),
        };

        args.Verbs.Add(verb);
    }

    private void StartCut(EntityUid target, EntityUid user, EntityUid used)
    {
        if (!TryComp<AndroidDiodeComponent>(target, out var comp))
            return;

        if (comp.IsCuttingInProgress || !comp.HasDiode)
            return;

        comp.IsCuttingInProgress = true;
        Dirty(target, comp);

        var itemName = Name(used);

        _popup.PopupEntity(
            Loc.GetString("android-diode-cut-popup", ("item", itemName)),
            target,
            PopupType.LargeCaution);

        var args = new DoAfterArgs(EntityManager, user, CutDuration,
            new AndroidDiodeCutDoAfterEvent(), target, target: target, used: used)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(args);
    }

    private void OnCutDoAfter(Entity<AndroidDiodeComponent> ent, ref AndroidDiodeCutDoAfterEvent args)
    {
        ref var comp = ref ent.Comp;

        if (args.Cancelled || args.Handled)
        {
            comp.IsCuttingInProgress = false;
            Dirty(ent, comp);
            return;
        }

        comp.HasDiode = false;
        comp.IsCuttingInProgress = false;

        Dirty(ent, comp);

        _appearance.SetData(ent, AndroidDiodeVisuals.DiodeRemoved, true);
    }
}
