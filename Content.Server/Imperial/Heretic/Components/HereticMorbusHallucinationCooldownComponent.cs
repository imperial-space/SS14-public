namespace Content.Server.Imperial.Heretic.Components;

/// <summary>
/// Перезарядка галлюцинаций от изучения открытого кодекса Морбуса у конкретного существа.
/// </summary>
[RegisterComponent]
public sealed partial class HereticMorbusHallucinationCooldownComponent : Component
{
    [ViewVariables]
    public TimeSpan NextHallucination;
}
