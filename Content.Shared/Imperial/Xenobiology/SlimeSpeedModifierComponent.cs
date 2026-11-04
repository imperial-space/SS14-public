using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>
/// Модификаторы скорости ксенобиологии: блюспейс/сепия плитка (turf slowdown), смола, сепиевый стабилизированный экстракт и т.д.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SlimeSpeedModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, float> Modifiers = new();
}
