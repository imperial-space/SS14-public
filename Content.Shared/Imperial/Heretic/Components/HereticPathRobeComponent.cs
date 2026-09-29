namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticPathRobeComponent : Component
{
    [DataField(required: true)]
    public HereticPath Path;
}
