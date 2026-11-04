using System.Linq;
using System.Reflection;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Examine;
using Content.Shared.Imperial.MachineParts;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MachineParts;

/// <summary>Уровни деталей машины по типам.</summary>
public sealed class ImperialMachineRatings
{
    public readonly Dictionary<ImperialStockPartType, List<int>> Tiers = new();

    /// <summary>Универсальные детали (стак «Manipulator») — 1-й уровень любого типа.</summary>
    public int Generic;

    /// <summary>Средний уровень деталей этого типа; без таких деталей — 1.</summary>
    public float Avg(ImperialStockPartType type)
    {
        return Tiers.TryGetValue(type, out var list) && list.Count > 0 ? (float) list.Average() : 1f;
    }

    /// <summary>Сумма уровней деталей этого типа (как цикл по component_parts в TG); без таких деталей — fallback.</summary>
    public int Sum(ImperialStockPartType type, int fallback)
    {
        return Tiers.TryGetValue(type, out var list) && list.Count > 0 ? list.Sum() : fallback;
    }

    public int Max(ImperialStockPartType type)
    {
        return Tiers.TryGetValue(type, out var list) && list.Count > 0 ? list.Max() : 1;
    }
}
