using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.BloodBrother;

/// <summary>datum/antagonist/brother: отметка на разуме брата со ссылкой на его команду.</summary>
[RegisterComponent]
public sealed partial class BloodBrotherComponent : Component
{
    /// <summary>Сущность игрового правила, на которой живёт команда.</summary>
    [ViewVariables]
    public EntityUid Team;
}
