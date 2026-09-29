namespace Content.Server.Imperial.Heretic.Components;

/// <summary>
/// Еретик рядом с разломом реальности: поле зрения временно переключено.
/// </summary>
[RegisterComponent]
public sealed partial class HereticRiftFovEffectComponent : Component
{
    /// <summary>Каким было DrawFov до эффекта.</summary>
    [ViewVariables]
    public bool OriginalDrawFov;

    [ViewVariables]
    public TimeSpan EndTime;
}
