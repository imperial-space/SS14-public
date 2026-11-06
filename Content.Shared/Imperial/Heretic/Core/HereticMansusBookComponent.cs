namespace Content.Shared.Imperial.Heretic.Core;

/// <summary>Книга Мансуса — физический предмет. При использовании в руке открывает меню знаний еретика.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HereticMansusBookComponent : Component
{
    [DataField]
    public TimeSpan OpeningAnimDuration = TimeSpan.FromSeconds(2.1f);

    [DataField]
    public TimeSpan ClosingAnimDuration = TimeSpan.FromSeconds(1.7f);

    [ViewVariables, AutoPausedField]
    public TimeSpan TransitionEnd;

    [ViewVariables]
    public HereticMansusBookVisualState? NextState;
}
