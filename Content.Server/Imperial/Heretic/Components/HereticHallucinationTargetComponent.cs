namespace Content.Server.Imperial.Heretic.Components;

/// <summary>
/// Активные последствия галлюцинации Плачущих у существа: размытие зрения и призрачная иллюзия.
/// </summary>
[RegisterComponent]
public sealed partial class HereticHallucinationTargetComponent : Component
{
    /// <summary>Когда вернуть зрение. Null — размытия сейчас нет.</summary>
    [ViewVariables]
    public TimeSpan? BlurEndTime;

    /// <summary>Призрак-иллюзия, который сейчас показан существу.</summary>
    [ViewVariables]
    public EntityUid? Ghost;
}
