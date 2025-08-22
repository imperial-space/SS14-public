using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared.Store;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.CustomChaplain.Components;

/// <summary>
/// Компонент магазина способностей кастомного священника.
/// </summary>
[RegisterComponent, AutoGenerateComponentState]
public sealed partial class CustomChaplainStoreComponent : Component
{
    /// <summary>
    /// Название магазина
    /// </summary>
    [DataField("name"), AutoNetworkedField]
    public string Name = "Магазин способностей";

    /// <summary>
    /// Баланс веры
    /// </summary>
    [DataField("faithBalance"), AutoNetworkedField]
    public int FaithBalance = 0;

    /// <summary>
    /// Категории товаров
    /// </summary>
    [DataField("categories"), AutoNetworkedField]
    public List<ProtoId<StoreCategoryPrototype>> Categories = new() { new ProtoId<StoreCategoryPrototype>("CustomChaplainAbilities") };
}
