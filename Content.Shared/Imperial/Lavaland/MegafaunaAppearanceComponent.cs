using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland;

/// <summary>Сущность меняет базовый слой по <see cref="MegafaunaVisuals"/>.</summary>
[RegisterComponent]
public sealed partial class MegafaunaAppearanceComponent : Component
{
    /// <summary>Состояние базового слоя, когда <see cref="MegafaunaVisuals.State"/> пуст.</summary>
    [DataField(required: true)]
    public string BaseState = string.Empty;
}
