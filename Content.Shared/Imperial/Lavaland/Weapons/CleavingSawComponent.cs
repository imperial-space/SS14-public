using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.Weapons;

/// <summary>
/// Изменяет AttackRate у MeleeWeapon при переключении режимов через ItemToggle.
/// Сложенный режим (deactivated): высокая скорость атаки.
/// Разложенный режим (activated): медленная атака с высоким уроном.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CleavingSawComponent : Component
{
    /// <summary>
    /// Частота атак в секунду в разложенном (activated) режиме. КД = 1 / значение.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ActivatedAttackRate = 1.0f;

    /// <summary>
    /// Частота атак в секунду в сложенном (deactivated) режиме. КД = 1 / значение.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeactivatedAttackRate = 2.5f;
}
