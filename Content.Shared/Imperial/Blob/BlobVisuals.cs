using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob;

[Serializable, NetSerializable]
public enum BlobVisuals : byte
{
    /// <summary>Цвет штамма (Color); у мёртвого блоба — белый.</summary>
    Color,

    /// <summary>Состояние основного слоя (string).</summary>
    State,

    /// <summary>Второй цвет (complementary color / цвет вен).</summary>
    SecondaryColor,

    /// <summary>Цвет глаз блоббернаута.</summary>
    EyesColor,

    /// <summary>Жив ли моб (bool) — скрывает вены и глаза у мёртвого блоббернаута.</summary>
    Alive,
}

public enum BlobVisualLayers : byte
{
    Base,
    Blob,
    Overlay,
    Veins,
    Eyes,
}
