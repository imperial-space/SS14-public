using Content.Shared.Actions;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticCarvingKnifeSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticCarvingKnifeComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HereticCarvingKnifeComponent, HereticCarvingAlertRuneActionEvent>(OnAlertRune);
        SubscribeLocalEvent<HereticCarvingKnifeComponent, HereticCarvingStunRuneActionEvent>(OnStunRune);
        SubscribeLocalEvent<HereticCarvingKnifeComponent, HereticCarvingMadnessRuneActionEvent>(OnMadnessRune);
    }

    private void OnGetActions(Entity<HereticCarvingKnifeComponent> ent, ref GetItemActionsEvent args)
    {
        if (!args.InHands || ent.Comp.Charges <= 0)
            return;
        args.AddAction(ref ent.Comp.AlertRuneActionEntity, ent.Comp.AlertRuneAction);
        args.AddAction(ref ent.Comp.StunRuneActionEntity, ent.Comp.StunRuneAction);
        args.AddAction(ref ent.Comp.MadnessRuneActionEntity, ent.Comp.MadnessRuneAction);
        Dirty(ent.Owner, ent.Comp);
    }

    private bool UseCharge(Entity<HereticCarvingKnifeComponent> ent)
    {
        if (ent.Comp.Charges <= 0)
            return false;
        ent.Comp.Charges--;
        if (ent.Comp.Charges <= 0)
        {
            _actions.RemoveAction(ent.Comp.AlertRuneActionEntity);
            _actions.RemoveAction(ent.Comp.StunRuneActionEntity);
            _actions.RemoveAction(ent.Comp.MadnessRuneActionEntity);
            ent.Comp.AlertRuneActionEntity = null;
            ent.Comp.StunRuneActionEntity = null;
            ent.Comp.MadnessRuneActionEntity = null;
        }
        Dirty(ent.Owner, ent.Comp);
        return true;
    }

    private void OnAlertRune(Entity<HereticCarvingKnifeComponent> ent, ref HereticCarvingAlertRuneActionEvent args)
    {
        args.Handled = true;
        if (!UseCharge(ent))
            return;
        var rune = Spawn("HereticCarvingAlertRune", args.Target);
        var alertComp = EnsureComp<HereticAlertRuneComponent>(rune);
        alertComp.HereticUid = args.Performer;
        Dirty(rune, alertComp);
    }

    private void OnStunRune(Entity<HereticCarvingKnifeComponent> ent, ref HereticCarvingStunRuneActionEvent args)
    {
        args.Handled = true;
        if (!UseCharge(ent))
            return;
        Spawn("HereticCarvingStunRune", args.Target);
    }

    private void OnMadnessRune(Entity<HereticCarvingKnifeComponent> ent, ref HereticCarvingMadnessRuneActionEvent args)
    {
        args.Handled = true;
        if (!UseCharge(ent))
            return;
        Spawn("HereticCarvingMadnessRune", args.Target);
    }
}
