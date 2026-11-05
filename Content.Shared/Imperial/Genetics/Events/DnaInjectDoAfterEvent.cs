using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Укол ДНК-инжектором.</summary>
[Serializable, Robust.Shared.Serialization.NetSerializable]
public sealed partial class DnaInjectDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
