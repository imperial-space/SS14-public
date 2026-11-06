using Content.Server.Imperial.Disease;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Vending;

/// <summary>Пробирка с готовой культурой болезни (reagent_containers/cup/bottle/&lt;болезнь&gt; TG).</summary>
[RegisterComponent]
public sealed partial class ImperialDiseaseCultureComponent : Component
{
    [DataField(required: true)]
    public ProtoId<DiseasePrototype> Disease;

    /// <summary>Кровь-носитель культуры.</summary>
    [DataField]
    public FixedPoint2 Blood = 20;
}
