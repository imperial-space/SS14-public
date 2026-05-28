using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Xenobiology.Systems;

/// <summary>
/// Система обнаружения инъекций реагентов в ксено-слаймов.
///
/// Алгоритм:
///   1. Подписываемся на SolutionContainerChangedEvent на сущностях с XenoSlimeComponent.
///   2. При изменении раствора «chemicals» ищем известные реагенты:
///      SlimeStabilizer, SlimeSteroid, XenoSlimeGrowthFactor.
///   3. Поднимаем XenoSlimeInjectedEvent на слайме, который подхватывает XenoSlimeSystem.
///   4. После обработки удаляем реагент из раствора (потребляем разовую дозу).
/// </summary>
public sealed class XenoSlimeInjectionSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    private static readonly string[] _trackedReagents =
    {
        "SlimeStabilizer",
        "SlimeSteroid",
        "XenoSlimeGrowthFactor",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenoSlimeComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
    }

    private void OnSolutionChanged(EntityUid uid, XenoSlimeComponent comp, ref SolutionContainerChangedEvent args)
    {
        // Нас интересует только раствор «chemicals» (именно туда шприц/гипоспрей делает инъекцию)
        if (args.SolutionId != "chemicals")
            return;

        if (!_solutions.TryGetSolution(uid, args.SolutionId, out var solutionEnt, out var solution))
            return;

        string? reagentToApply = null;
        var dosesToApply = 0;

        foreach (var reagentId in _trackedReagents)
        {
            var reagent = new ReagentId(reagentId, null);
            var oneDose = FixedPoint2.New(1);
            var qty = solution.GetReagentQuantity(reagent);
            var doses = (int) (qty / oneDose);

            if (doses <= 0)
                continue;

            reagentToApply = reagentId;
            dosesToApply = doses;
            break;
        }

        if (reagentToApply == null || dosesToApply <= 0)
            return;

        for (var i = 0; i < dosesToApply; i++)
        {
            // Поднимаем событие на каждую полную дозу.
            var ev = new XenoSlimeInjectedEvent { ReagentId = reagentToApply };
            RaiseLocalEvent(uid, ev);
        }

        // Расходуем из реального контейнера, а не из снапшота args.Solution.
        _solutions.RemoveReagent(solutionEnt.Value, reagentToApply, FixedPoint2.New(dosesToApply));
    }
}
