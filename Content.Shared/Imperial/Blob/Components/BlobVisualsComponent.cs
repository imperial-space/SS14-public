using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob.Components;

[Serializable, NetSerializable]
public enum BlobVisualsMode : byte
{
    /// <summary>Структура: основной слой — состояние и цвет; у ядра/узла цветной слой Blob.</summary>
    Structure,

    /// <summary>Окрашивается весь спрайт (споры, эффекты, оверманд).</summary>
    Tint,

    /// <summary>Блоббернаут: тело, вены и глаза раздельно.</summary>
    Blobbernaut,

    /// <summary>Зомби: окрашена только голова блоба.</summary>
    Zombie,
}

/// <summary>Как клиент окрашивает спрайт по <see cref="BlobVisuals"/>.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BlobVisualsComponent : Component
{
    [DataField]
    public BlobVisualsMode Mode = BlobVisualsMode.Structure;
}
