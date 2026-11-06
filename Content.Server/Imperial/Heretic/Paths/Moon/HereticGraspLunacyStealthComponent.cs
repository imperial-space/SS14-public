namespace Content.Server.Imperial.Heretic.Paths.Moon;

/// <summary>
/// Временная невидимость еретика Луны после Хватки Мансуса.
/// </summary>
[RegisterComponent]
public sealed partial class HereticGraspLunacyStealthComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan EndTime;
}
