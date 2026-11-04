namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Друг слайма: его указания пальцем передаются слаймам (pet_command attack).</summary>
[RegisterComponent]
public sealed partial class SlimeCommanderComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> Slimes = new();
}
