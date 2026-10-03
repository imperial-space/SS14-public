using System.Linq;
using System.Text;
using Content.Server.Actions;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Decals;
using Content.Server.Doors.Systems;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Antag;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Decals;
using Content.Shared.Doors.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Follower.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Prototypes;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Overlays;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Speech.Muting;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using CancellationTokenSource = System.Threading.CancellationTokenSource;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Книга Мансуса и интерфейс знаний.
/// </summary>
public sealed partial class HereticSystem
{
    private void OnKnowledgeMenu(EntityUid uid, HereticComponent comp, HereticKnowledgeMenuActionEvent args)
    {
        args.Handled = true;
        if (comp.BuiHolder == EntityUid.Invalid || !Exists(comp.BuiHolder))
            return;

        OpenInfoBui(uid, comp);
    }

    private void OnMansusBookUseInHand(EntityUid uid, HereticMansusBookComponent comp, UseInHandEvent args)
    {
        if (!TryComp<HereticComponent>(args.User, out var hereticComp))
            return;

        args.Handled = true;
        hereticComp.MansusBook = uid;
        if (hereticComp.BuiHolder == EntityUid.Invalid || !Exists(hereticComp.BuiHolder))
            return;

        OpenInfoBui(args.User, hereticComp);

        comp.NextState = HereticMansusBookVisualState.Open;
        comp.TransitionTimeRemaining = comp.OpeningAnimDuration;
        _appearance.SetData(uid, HereticMansusBookVisuals.State, HereticMansusBookVisualState.Opening);
    }

    private void OnHolderBuiClosed(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, BoundUIClosedEvent args)
    {
        if (!TryComp<HereticComponent>(args.Actor, out var hereticComp))
            return;

        var book = hereticComp.MansusBook;
        if (book == EntityUid.Invalid || !Exists(book) || !TryComp<HereticMansusBookComponent>(book, out var bookComp))
            return;

        bookComp.NextState = HereticMansusBookVisualState.Closed;
        bookComp.TransitionTimeRemaining = bookComp.ClosingAnimDuration;
        _appearance.SetData(book, HereticMansusBookVisuals.State, HereticMansusBookVisualState.Closing);
    }

    private void OpenInfoBui(EntityUid uid, HereticComponent comp)
    {
        SendInfoBuiState(uid, comp);
        _ui.TryOpenUi(comp.BuiHolder, HereticInfoBuiKey.Key, uid);
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
        SendInfoBuiState(args.Actor, heretic);
    }

    public void SendInfoBuiState(EntityUid uid, HereticComponent comp)
    {
        if (comp.BuiHolder == EntityUid.Invalid || !Exists(comp.BuiHolder))
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

        _ui.SetUiState(comp.BuiHolder, HereticInfoBuiKey.Key, new HereticInfoBuiState
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
