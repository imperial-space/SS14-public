using Content.Shared.Imperial.Heretic;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticPathRobeComponent : Component
{
    [DataField(required: true)]
    public HereticPath Path;
}
