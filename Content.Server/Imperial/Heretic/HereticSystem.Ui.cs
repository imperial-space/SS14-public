using System.Linq;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Prototypes;
using Content.Shared.Interaction.Events;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Книга Мансуса и интерфейс знаний.
/// </summary>
public sealed partial class HereticSystem
{
    private void OnKnowledgeMenu(EntityUid uid, HereticComponent comp, HereticKnowledgeMenuActionEvent args)
    {
        args.Handled = true;
        if (comp.BuiHolder == null || !Exists(comp.BuiHolder))
            return;

        OpenInfoBui(uid, comp);
    }

    private void OnMansusBookUseInHand(EntityUid uid, HereticMansusBookComponent comp, UseInHandEvent args)
    {
        if (!TryComp<HereticComponent>(args.User, out var hereticComp))
            return;

        args.Handled = true;
        hereticComp.MansusBook = uid;
        Dirty(args.User, hereticComp);
        if (hereticComp.BuiHolder == null || !Exists(hereticComp.BuiHolder))
            return;

        OpenInfoBui(args.User, hereticComp);

        comp.NextState = HereticMansusBookVisualState.Open;
        comp.TransitionEnd = _timing.CurTime + comp.OpeningAnimDuration;
        _appearance.SetData(uid, HereticMansusBookVisuals.State, HereticMansusBookVisualState.Opening);
    }

    private void OnHolderBuiClosed(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, BoundUIClosedEvent args)
    {
        if (!TryComp<HereticComponent>(args.Actor, out var hereticComp))
            return;

        var book = hereticComp.MansusBook;
        if (book is not { } bookUid || !TryComp<HereticMansusBookComponent>(bookUid, out var bookComp))
            return;

        bookComp.NextState = HereticMansusBookVisualState.Closed;
        bookComp.TransitionEnd = _timing.CurTime + bookComp.ClosingAnimDuration;
        _appearance.SetData(bookUid, HereticMansusBookVisuals.State, HereticMansusBookVisualState.Closing);
    }

    private void OpenInfoBui(EntityUid uid, HereticComponent comp)
    {
        SendInfoBuiState(uid, comp);
        if (comp.BuiHolder is { } holder)
            _ui.TryOpenUi(holder, HereticInfoBuiKey.Key, uid);
    }

    private void OnSelectPathMessage(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, HereticSelectPathMessage args)
    {
        if (!TryComp<HereticComponent>(args.Actor, out var comp))
            return;
        if (comp.CurrentPath != HereticPath.General)
            return;

        TryResearchKnowledge(args.Actor, comp, args.KnowledgeId);
        comp.PassiveLevel = 1;

        if (comp.CurrentPath == HereticPath.Ash)
            _ashPassive.ApplyPassiveLevel1(args.Actor);

        if (comp.CurrentPath == HereticPath.Moon)
            _moonPassive.ApplyPassiveLevel1(args.Actor);

        if (comp.CurrentPath == HereticPath.Lock)
            _lockPassive.ApplyPassiveLevel1(args.Actor);

        if (comp.CurrentPath == HereticPath.Blade)
            _bladePassive.ApplyPassiveLevel1(args.Actor);

        if (comp.CurrentPath == HereticPath.Flesh)
            GrantKnowledge(args.Actor, comp, "KnowledgeMarkOfFlesh");

        if (comp.CurrentPath == HereticPath.Void)
            _voidPassive.ApplyPassiveLevel1(args.Actor);

        if (comp.CurrentPath == HereticPath.Rust)
            _rustPassive.ApplyPassiveLevel1(args.Actor);

        if (comp.CurrentPath == HereticPath.Cosmos)
            _cosmosPassive.ApplyPassiveLevel1(args.Actor);

        Dirty(args.Actor, comp);
        SendInfoBuiState(args.Actor, comp);
        _ui.TryOpenUi(holderUid, HereticInfoBuiKey.Key, args.Actor);
    }

    private void OnResearchMessage(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, HereticResearchKnowledgeMessage args)
    {
        if (!TryComp<HereticComponent>(args.Actor, out var comp))
            return;

        TryResearchKnowledge(args.Actor, comp, args.KnowledgeId);
    }

    private void OnBuiRangeCheck(EntityUid uid, HereticKnowledgeHolderComponent comp, BoundUserInterfaceCheckRangeEvent args)
    {
        args.Result = BoundUserInterfaceRangeResult.Pass;
    }

    private void OnDenyAscensionMessage(EntityUid uid, HereticKnowledgeHolderComponent comp, HereticDenyAscensionMessage args)
    {
        if (!TryComp<HereticComponent>(args.Actor, out var heretic))
            return;
        heretic.AscensionDenied = true;
        Dirty(args.Actor, heretic);
        SendInfoBuiState(args.Actor, heretic);
    }

    public void SendInfoBuiState(EntityUid uid, HereticComponent comp)
    {
        if (comp.BuiHolder == null || !Exists(comp.BuiHolder))
            return;

        var nodes = new List<HereticKnowledgeNodeData>();
        foreach (var proto in _proto.EnumeratePrototypes<HereticKnowledgePrototype>())
        {
            var prereqsMet = ArePrerequisitesMet(comp, proto);
            nodes.Add(new HereticKnowledgeNodeData
            {
                Id = proto.ID,
                Name = Loc.GetString(proto.Name),
                Description = Loc.GetString(proto.Description),
                Cost = proto.Cost,
                Path = proto.Path,
                IsResearched = comp.ResearchedKnowledge.Contains(proto.ID),
                PrerequisitesMet = prereqsMet,
                Icon = proto.Icon,
                Prerequisites = proto.Prerequisites.Select(p => p.Id).ToList(),
                PrerequisitesAny = proto.PrerequisitesAny.Select(set => set.Select(p => p.Id).ToList()).ToList(),
                IsGift = proto.IsGift,
                ShopLevel = proto.ShopLevel,
                ConflictsWith = proto.ConflictsWith.Select(p => p.Id).ToList(),
            });
        }

        var paths = new List<HereticPathData>();
        foreach (var proto in _proto.EnumeratePrototypes<HereticPathPrototype>())
        {
            paths.Add(new HereticPathData
            {
                Id = proto.ID,
                Path = proto.Path,
                Name = Loc.GetString(proto.Name),
                Description = Loc.GetString(proto.Description),
                Complexity = Loc.GetString(proto.Complexity),
                PassiveName = Loc.GetString(proto.PassiveName),
                PassiveDescription = Loc.GetString(proto.PassiveDescription),
                Pros = Loc.GetString(proto.Pros),
                Cons = string.IsNullOrEmpty(proto.Cons) ? string.Empty : Loc.GetString(proto.Cons),
                Level1Description = string.IsNullOrEmpty(proto.Level1Description) ? string.Empty : Loc.GetString(proto.Level1Description),
                Level2Description = string.IsNullOrEmpty(proto.Level2Description) ? string.Empty : Loc.GetString(proto.Level2Description),
                Level3Description = string.IsNullOrEmpty(proto.Level3Description) ? string.Empty : Loc.GetString(proto.Level3Description),
                Icon = proto.Icon,
                PathKnowledgeId = proto.PathKnowledgeId,
                Tips = string.IsNullOrEmpty(proto.Tips) ? string.Empty : Loc.GetString(proto.Tips),
            });
        }
        paths.Sort((a, b) => (int)a.Path - (int)b.Path);

        _ui.SetUiState(comp.BuiHolder.Value, HereticInfoBuiKey.Key, new HereticInfoBuiState
        {
            KnowledgePoints = comp.KnowledgePoints,
            CurrentPath = comp.CurrentPath,
            PassiveLevel = comp.PassiveLevel,
            SacrificeCount = comp.SacrificeCount,
            RequiredSacrifices = comp.RequiredSacrifices,
            RequiredKnowledge = comp.RequiredKnowledge,
            Paths = paths,
            Nodes = nodes,
            CurrentShopLevel = comp.ShopLevel,
            PendingGiftGroups = comp.PendingGiftGroups.Select(g => new HereticGiftGroup { SourceNodeId = g.SourceNodeId, Candidates = new List<string>(g.Candidates) }).ToList(),
        });
    }

}
