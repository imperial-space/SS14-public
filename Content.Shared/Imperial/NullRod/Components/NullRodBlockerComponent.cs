using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Серверная метка на держащем блокирующее святое оружие. Хранит удар, который решено отбить.
/// </summary>
[RegisterComponent]
public sealed partial class NullRodBlockerComponent : Component
{
    /// <summary>Атакующий, чей удар ближнего боя в этот тик будет отбит.</summary>
    [ViewVariables]
    public EntityUid? BlockedAttacker;

    [ViewVariables]
    public EntityUid? BlockingWeapon;

    [ViewVariables]
    public GameTick BlockTick;
}
