using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.VatGrowing;

/// <summary>Цвет пробы (GLOB.xeno_sample_colors) и состояние чана для визуализаторов.</summary>
[Robust.Shared.Serialization.NetSerializable, Serializable]
public enum VatGrowingVisuals : byte
{
    /// <summary>Строка: brown, gray, green, purple, yellow или none.</summary>
    SampleColor,
    /// <summary>В чане есть жидкость.</summary>
    HasReagents,
    /// <summary>Ресемплер включён.</summary>
    Resampler,
    /// <summary>В чане растёт проба.</summary>
    Growing,
}
