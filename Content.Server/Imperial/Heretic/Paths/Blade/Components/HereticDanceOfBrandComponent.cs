namespace Content.Server.Imperial.Heretic.Paths.Blade.Components;

/// <summary>
/// Перезарядка пассивной контратаки «Танец клейма» у еретика пути Клинка.
/// </summary>
[RegisterComponent, Access(typeof(HereticBladeActionsSystem))]
public sealed partial class HereticDanceOfBrandComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public TimeSpan NextTrigger;
}
