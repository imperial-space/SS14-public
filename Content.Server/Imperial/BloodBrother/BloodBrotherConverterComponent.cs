using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.BloodBrother;

/// <summary>
/// grant_conversion_skills(): пока команда может пробудить ещё брата, носитель
/// вербует вспышкой. Висит на теле брата.
/// </summary>
[RegisterComponent]
public sealed partial class BloodBrotherConverterComponent : Component
{
    [ViewVariables]
    public EntityUid Team;
}
