using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>
/// Части костюма, которые должны быть развёрнуты для работы модуля (required_slots в TG).
/// Перчатки и ботинки МОДов сборки не разворачиваются отдельно, их заменяет нагрудник.
/// </summary>
[Flags]
public enum ImperialModPart : byte
{
    None = 0,
    Back = 1 << 0,
    Outer = 1 << 1,
    Head = 1 << 2,
}
