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

public sealed class ImperialDiseaseCultureSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DiseaseSystem _disease = default!;
    [Dependency] private readonly SolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialDiseaseCultureComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<ImperialDiseaseCultureComponent> ent, ref MapInitEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.Disease, out var proto))
            return;

        EnsureComp<DiseaseSampleComponent>(ent).Diseases.Add(_disease.CreateFromPrototype(proto));

        if (!TryComp<SolutionContainerManagerComponent>(ent, out var manager))
            return;

        foreach (var (_, solution) in _solutions.EnumerateSolutions((ent, manager)))
        {
            _solutions.TryAddReagent(solution, "Blood", ent.Comp.Blood, out _);
            return;
        }
    }
}
