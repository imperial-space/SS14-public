using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Укрытие: мешки с песком. Стрелок, стоящий вплотную, стреляет поверх, а издали пули то застревают, то проходят.
/// </summary>
[RegisterComponent]
public sealed partial class CadiaCoverComponent : Component
{
    /// <summary>Какая доля пуль издали застревает в укрытии (0..1).</summary>
    [DataField]
    public float Block = 0.65f;

    /// <summary>С какого расстояния стрелок уже стреляет поверх.</summary>
    [DataField]
    public float OverRange = 1.7f;

    /// <summary>ДОТ, которому принадлежат стены: изнутри пули проходят всегда, снаружи почти всегда застревают.</summary>
    [ViewVariables]
    public EntityUid? Inside;
}
