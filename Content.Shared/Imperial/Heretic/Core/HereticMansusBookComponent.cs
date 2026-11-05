namespace Content.Shared.Imperial.Heretic.Core;

/// <summary>Книга Мансуса — физический предмет. При использовании в руке открывает меню знаний еретика.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HereticMansusBookComponent : Component
{
    [DataField]
    public float OpeningAnimDuration = 2.1f;

    [DataField]
    public float ClosingAnimDuration = 1.7f;

    [ViewVariables, AutoPausedField]
    public TimeSpan TransitionEnd;

    [ViewVariables]
    public HereticMansusBookVisualState? NextState;
}
