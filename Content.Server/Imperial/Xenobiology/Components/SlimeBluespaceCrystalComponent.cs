namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>stack/ore/bluespace_crystal: телепорт на blink_range при использовании и броске.</summary>
[RegisterComponent]
public sealed partial class SlimeBluespaceCrystalComponent : Component
{
    [DataField]
    public int BlinkRange = 8;
}
