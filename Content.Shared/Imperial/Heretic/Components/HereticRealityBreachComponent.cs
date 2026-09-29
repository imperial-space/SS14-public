namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Marks a Reality Breach — the visible-to-all remnant that fades in after a rift is absorbed.
/// </summary>
[RegisterComponent]
public sealed partial class HereticRealityBreachComponent : Component
{
    /// <summary>За сколько секунд прорыв полностью проявляется.</summary>
    [DataField]
    public TimeSpan FadeDuration = TimeSpan.FromSeconds(4);

    /// <summary>Клиентское: когда началось проявление. Null — проявление закончено.</summary>
    [ViewVariables]
    public TimeSpan? FadeStartTime;
}
