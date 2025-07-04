using System.Reflection.Metadata;
using Content.Shared.Friends;
using Content.Shared.Friends.Prototypes;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Friends.UI;

[UsedImplicitly]
public sealed class FactionRelationsUiController : UIController
{
    public void OpenSetRelationMenu(NetEntity target, ProtoId<MedievalFactionPrototype> userFaction, ProtoId<MedievalFactionPrototype> targetFaction)
    {
        var menu = new SetFactionRelationsMenu(userFaction, targetFaction);
        menu.SendPressed += (_, _, args) => Offer(target, userFaction, targetFaction, args);
        menu.OpenCentered();
    }

    public void OpenAcceptMenu(ProtoId<MedievalFactionPrototype> userFaction, ProtoId<MedievalFactionPrototype> targetFaction, ProtoId<FactionRelationsPrototype> relation)
    {
        var menu = new AcceptFactionRelationsMenu(userFaction, relation);
        menu.SendPressed += () => SetFactionRelation(userFaction, targetFaction, relation);
        menu.OpenCentered();
    }

    private void SetFactionRelation(ProtoId<MedievalFactionPrototype> userFaction, ProtoId<MedievalFactionPrototype> targetFaction, ProtoId<FactionRelationsPrototype> relation)
    {
        var ev = new AcceptFactionRelationsEvent(userFaction, targetFaction, relation);
        EntityManager.RaisePredictiveEvent(ev);
    }

    private void Offer(NetEntity target, ProtoId<MedievalFactionPrototype> userFaction, ProtoId<MedievalFactionPrototype> targetFaction, ProtoId<FactionRelationsPrototype> relation)
    {
        var ev = new OfferFactionRelationsEvent(target, userFaction, targetFaction, relation);
        EntityManager.RaisePredictiveEvent(ev);
    }
}
