using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Vision;

/// <summary>
/// Ночное зрение: освещение и тени остаются, но в темноте всё видно.
/// Работает и на самом существе, и на надетой одежде.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialNightVisionComponent : Component
{
    /// <summary>Минимальная яркость неосвещённых мест (sRGB). Чем светлее, тем лучше видно в тенях.</summary>
    [DataField]
    public Color MinimumLight = Color.FromHex("#c8c8c8");
}
