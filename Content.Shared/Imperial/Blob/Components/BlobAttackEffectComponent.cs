using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Blob.Components;

/// <summary>
/// temp_visual/blob + do_attack_animation(): вспышка делает выпад в сторону цели и возвращается.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BlobAttackEffectComponent : Component
{
    /// <summary>Смещение выпада в тайлах (8 px по каждой оси к цели).</summary>
    [DataField, AutoNetworkedField]
    public Vector2 Lunge;
}
