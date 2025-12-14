using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.IdentityManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Factions;

public abstract partial class SharedMedievalFactionsSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MedievalFactionMemberComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<MedievalFactionMemberComponent, OpenFactionMenuActionEvent>(OnFactionMenuAction);
    }

    private void OnExamine(EntityUid uid, MedievalFactionMemberComponent comp, ExaminedEvent args)
    {
        if (!TryComp<MedievalFactionMemberComponent>(args.Examiner, out var me) || uid == args.Examiner)
            return;
        if (!Identity.Name(uid, EntityManager).Equals(Name(uid)))
            return;
        if (!TryGetFactionMemberData(comp.MemberID, out var data))
            return;

        var myFaction = Proto.Index(me.Faction);
        var otherFaction = Proto.Index(comp.Faction);

        if (myFaction == otherFaction && myFaction.ShowKnown)
            args.PushMarkup("[color=green]Из моей фракции, это [/color] " + data.Job.ToLower());
        else if (myFaction.KnownFactions.TryGetValue(comp.Faction, out var str))
            args.PushMarkup(str);

        if (comp.Wanted != null && comp.Wanted.Value.Key == me.Faction)
            args.PushMarkup(comp.Wanted.Value.Value);
    }

    private void OnFactionMenuAction(EntityUid uid, MedievalFactionMemberComponent comp, OpenFactionMenuActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        if (!TryGetFactionDataContainer(out var container))
            return;

        OpenMenu(comp.Faction, container.Value.Comp.CachedMembers.GetOrNew(comp.Faction), comp.MenuAccess);
    }

    public virtual void OpenMenu(ProtoId<MedievalFactionPrototype> proto, Dictionary<int, FactionMemberData> data, FactionMenuAccess access)
    {
    }
}
