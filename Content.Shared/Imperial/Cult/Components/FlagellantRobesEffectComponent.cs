using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// Ставится на носителя мантии флагелланта.
/// Умножает весь входящий урон от внешних источников.
/// </summary>
[RegisterComponent]
public sealed partial class FlagellantRobesEffectComponent : Component
{
    /// <summary>Множитель урона, копируется из <see cref="FlagellantRobesComponent"/> при надевании.</summary>
    [DataField]
    public float DamageMultiplier = 2.0f;
}
