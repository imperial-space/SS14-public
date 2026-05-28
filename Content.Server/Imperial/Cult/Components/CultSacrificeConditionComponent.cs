using Content.Server.Objectives.Systems;
using Robust.Shared.ViewVariables;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>
/// Цель культа: принести N жертв через руну жертвоприношения.
/// Прогресс отслеживается через <see cref="CultSacrificeConditionSystem"/>.
/// </summary>
[RegisterComponent, Access(typeof(CultSacrificeConditionSystem))]
public sealed partial class CultSacrificeConditionComponent : Component
{
    /// <summary>
    /// Сколько жертв нужно принести для выполнения цели.
    /// </summary>
    [DataField]
    public int RequiredCount = 1;

    /// <summary>
    /// Сколько жертв уже принесено (отслеживается системой).
    /// </summary>
    [ViewVariables]
    public int CurrentCount;
}
