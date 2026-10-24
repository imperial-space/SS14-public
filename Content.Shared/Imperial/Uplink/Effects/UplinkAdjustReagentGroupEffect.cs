using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Uplink.Effects;

/// <summary>
/// Выводит из кровотока все реагенты указанной группы (анацея выводит лекарства, спевиум — токсины).
/// </summary>
public sealed partial class UplinkAdjustReagentGroupEntityEffectSystem : EntityEffectSystem<BloodstreamComponent, UplinkAdjustReagentGroup>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    protected override void Effect(Entity<BloodstreamComponent> entity, ref EntityEffectEvent<UplinkAdjustReagentGroup> args)
    {
        var amount = -args.Effect.Amount * args.Scale;
        if (amount <= 0
            || !_solutionContainer.ResolveSolution(entity.Owner, entity.Comp.BloodSolutionName, ref entity.Comp.BloodSolution, out var solution))
            return;

        for (var i = solution.Contents.Count - 1; i >= 0; i--)
        {
            var (reagentId, _) = solution.Contents[i];
            if (reagentId.Prototype == args.Effect.Exclude)
                continue;

            if (!_proto.TryIndex<ReagentPrototype>(reagentId.Prototype, out var proto) || proto.Group != args.Effect.Group)
                continue;

            _solutionContainer.RemoveReagent(entity.Comp.BloodSolution!.Value, reagentId, amount);
        }
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class UplinkAdjustReagentGroup : EntityEffectBase<UplinkAdjustReagentGroup>
{
    [DataField(required: true)]
    public string Group = string.Empty;

    /// <summary>Отрицательное — сколько вывести каждого реагента группы.</summary>
    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField]
    public string? Exclude;
}
