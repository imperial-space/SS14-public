using Content.Shared.Imperial.Heretic.Core;

namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent]
public sealed partial class HereticPathRobeComponent : Component
{
    [DataField(required: true)]
    public HereticPath Path;
}
