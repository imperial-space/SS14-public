namespace Content.Server.Imperial.Heretic.Components;

/// <summary>
/// Сколько раз не-еретик трогал и разглядывал прорывы реальности за последнее окно времени.
/// </summary>
[RegisterComponent]
public sealed partial class HereticBreachInteractionComponent : Component
{
    [ViewVariables]
    public int TouchCount;

    [ViewVariables]
    public TimeSpan TouchWindowStart;

    [ViewVariables]
    public int ExamineCount;

    [ViewVariables]
    public TimeSpan ExamineWindowStart;
}
