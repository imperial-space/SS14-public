using Content.Shared.Imperial.Heretic;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>Книга Мансуса — физический предмет. При использовании в руке открывает меню знаний еретика.</summary>
[RegisterComponent]
public sealed partial class HereticMansusBookComponent : Component
{
    [DataField]
    public float OpeningAnimDuration = 2.1f;

    [DataField]
    public float ClosingAnimDuration = 1.7f;

    public float TransitionTimeRemaining;

    public HereticMansusBookVisualState? NextState;
}
