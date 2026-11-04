using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics;

/// <summary>
/// Распад ДНК довёл до срыва (something_horrible из SS13).
/// </summary>
/// <param name="Instability">Насколько стабильность ушла ниже нуля: чем больше, тем вероятнее смертельный срыв.</param>
[ByRefEvent]
public record struct GenomeMeltdownEvent(int Instability);
