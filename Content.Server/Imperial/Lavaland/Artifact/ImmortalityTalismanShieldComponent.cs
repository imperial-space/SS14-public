namespace Content.Server.Imperial.Lavaland.Artifact;

/// <summary>
/// Неуязвимость от талисмана бессмертия: входящий урон не проходит до <see cref="EndTime"/>.
/// В отличие от режима бога SS14, не лечит при включении.
/// </summary>
[RegisterComponent]
public sealed partial class ImmortalityTalismanShieldComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;
}
