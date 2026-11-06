using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Сканирование генетического облика ручным сканером последовательностей.</summary>
[Serializable, Robust.Shared.Serialization.NetSerializable]
public sealed partial class SequenceScannerMakeupDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
