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

    private IPrototypeManager? _protoManager;
    private IPlacementManager? _placementManager;
    private ConstructionSystem? _constructionSystem;

    public CultConstructionBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _protoManager = IoCManager.Resolve<IPrototypeManager>();
        var entSysMan = IoCManager.Resolve<IEntitySystemManager>();
        _constructionSystem = entSysMan.GetEntitySystem<ConstructionSystem>();
        _placementManager = IoCManager.Resolve<IPlacementManager>();

        _window = this.CreateWindow<CultConstructionSelectWindow>();

        _window.OnStructureSelected += constructionId =>
        {
            if (_protoManager == null || _placementManager == null || _constructionSystem == null)
                return;

            if (!_protoManager.TryIndex<ConstructionPrototype>(constructionId, out var proto))
                return;

            _placementManager.BeginPlacing(
                new PlacementInformation { IsTile = false, PlacementOption = proto.PlacementMode },
                new ConstructionPlacementHijack(_constructionSystem, proto));
        };
    }
}