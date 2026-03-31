namespace Content.Server.Imperial.Cult.Components;

/// <summary>
/// Компонент тёмного духа — хранит ссылку на исходное тело культиста.
/// Добавляется к сущности MobCultDarkSpirit при вознесении.
/// </summary>
[RegisterComponent]
public sealed partial class CultDarkSpiritComponent : Component
{
    /// <summary>Тело культиста, из которого вознёсся дух.</summary>
    [DataField]
    public EntityUid OriginalBody { get; set; }
}
