using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Мутация включилась у носителя.</summary>
[ByRefEvent]
public readonly record struct MutationActivatedEvent(ProtoId<GeneticMutationPrototype> Mutation, ChromosomeKind Chromosome);

/// <summary>Мутация снята с носителя.</summary>
[ByRefEvent]
public readonly record struct MutationDeactivatedEvent(ProtoId<GeneticMutationPrototype> Mutation);

/// <summary>
/// Распад ДНК довёл до срыва (something_horrible из SS13).
/// </summary>
/// <param name="Instability">Насколько стабильность ушла ниже нуля: чем больше, тем вероятнее смертельный срыв.</param>
[ByRefEvent]
public record struct GenomeMeltdownEvent(int Instability);

/// <summary>Укол ДНК-инжектором.</summary>
[Serializable, Robust.Shared.Serialization.NetSerializable]
public sealed partial class DnaInjectDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

/// <summary>Инфузия ДНК в ДНК-инфузере.</summary>
[Serializable, Robust.Shared.Serialization.NetSerializable]
public sealed partial class DnaInfuseDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

/// <summary>Сканирование генетического облика ручным сканером последовательностей.</summary>
[Serializable, Robust.Shared.Serialization.NetSerializable]
public sealed partial class SequenceScannerMakeupDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
