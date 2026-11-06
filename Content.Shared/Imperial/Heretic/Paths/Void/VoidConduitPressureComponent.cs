namespace Content.Shared.Imperial.Heretic.Paths.Void;

[RegisterComponent]
public sealed partial class VoidConduitPressureComponent : Component
{
    [ViewVariables]
    public TimeSpan ExpiresAt;
}
