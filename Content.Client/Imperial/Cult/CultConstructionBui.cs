using Content.Client.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Client.Placement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Cult;

public sealed class CultConstructionBui : BoundUserInterface
{
    [ViewVariables]
    private CultConstructionSelectWindow? _window;

    public CultConstructionBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CultConstructionSelectWindow>();

        _window.OnStructureSelected += constructionId =>
        {
            var protoManager = IoCManager.Resolve<IPrototypeManager>();
            if (!protoManager.TryIndex<ConstructionPrototype>(constructionId, out var proto))
                return;

            var entSysMan = IoCManager.Resolve<IEntitySystemManager>();
            var constructionSystem = entSysMan.GetEntitySystem<ConstructionSystem>();
            var placementManager = IoCManager.Resolve<IPlacementManager>();

            placementManager.BeginPlacing(
                new PlacementInformation { IsTile = false, PlacementOption = proto.PlacementMode },
                new ConstructionPlacementHijack(constructionSystem, proto));
        };
    }
}