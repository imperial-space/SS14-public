namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>obj/effect/timestop: всё в радиусе замирает.</summary>
[RegisterComponent]
public sealed partial class SlimeTimestopComponent : Component
{
    [DataField]
    public float Range = 2f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(14);

    [ViewVariables]
    public TimeSpan End;

    [ViewVariables]
    public TimeSpan NextTick;

    /// <summary>Кто не замирает (TRAIT_TIME_STOP_IMMUNE у владельца эффекта).</summary>
    [ViewVariables]
    public HashSet<EntityUid> Immune = new();

    [DataField]
    public TimeSpan TickDelay = TimeSpan.FromSeconds(0.1);
}
