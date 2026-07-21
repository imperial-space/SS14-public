using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Medieval.Achievements;

[RegisterComponent]
public sealed partial class AchievementLocationComponent : Component
{
    [DataField(required: true)]
    public string LocationId = default!;
}
