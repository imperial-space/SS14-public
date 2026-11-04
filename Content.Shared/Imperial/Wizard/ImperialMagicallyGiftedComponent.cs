using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Маг или ученик (TRAIT_MAGICALLY_GIFTED): видит освящённые клетки, может пользоваться магическими предметами.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialMagicallyGiftedComponent : Component;
