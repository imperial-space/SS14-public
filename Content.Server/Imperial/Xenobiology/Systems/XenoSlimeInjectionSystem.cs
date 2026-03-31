using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
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
    private static readonly string[] TrackedReagents =
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

        foreach (var reagentId in TrackedReagents)
        {
            var qty = args.Solution.GetReagentQuantity(
                new Content.Shared.Chemistry.Reagent.ReagentId(reagentId, null));

            if (qty <= 0)
                continue;

            // Поднимаем событие – XenoSlimeSystem обработает изменение состояния
            var ev = new XenoSlimeInjectedEvent { ReagentId = reagentId };
            RaiseLocalEvent(uid, ev);

            // Потребляем реагент из раствора (1 единица = 1 доза)
            args.Solution.RemoveReagent(reagentId, qty);
        }
    }
}
