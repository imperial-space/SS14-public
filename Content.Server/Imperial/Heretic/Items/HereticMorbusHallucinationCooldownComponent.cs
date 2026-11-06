namespace Content.Server.Imperial.Heretic.Items;

/// <summary>
/// Перезарядка галлюцинаций от изучения открытого кодекса Морбуса у конкретного существа.
/// </summary>
[RegisterComponent]
public sealed partial class HereticMorbusHallucinationCooldownComponent : Component
{
    [ViewVariables]
    public TimeSpan NextHallucination;
}
