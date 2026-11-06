namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent]
public sealed partial class HereticEtherFlaskComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan Cooldown2 = TimeSpan.FromSeconds(60);
}
